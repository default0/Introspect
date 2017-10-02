using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Introspect
{
	/// <summary>
	/// Allows to use all public methods required by TInterface if provided a suitable implementing object. The provided implementation only needs to expose public methods matching the interface, it does NOT need to explicitly implement it.
	/// </summary>
	public static class DuckInterface<TInterface>
		where TInterface : class
	{
		static DuckInterface()
		{
			if (!typeof(TInterface).IsInterface)
				throw new Exception($"The provided type {typeof(TInterface).FullName} is not an interface.");
			if (typeof(TInterface).GetCustomAttribute<StaticAttribute>() != null)
				throw new Exception($"The provided interface {typeof(TInterface).FullName} is marked with the {typeof(StaticAttribute).FullName} attribute. Duck typing does not work with static interfaces.");
		}

		private static Dictionary<Type, Func<object, TInterface>> duckCache = new Dictionary<Type, Func<object, TInterface>>();
		/// <summary>
		/// Creates an instance of a wrapper that wraps impl and forwards calls of interface methods to calls to impl.
		/// </summary>
		/// <param name="impl">The implementation that should be wrapped in an interface.</param>
		/// <returns>An instance of a wrapper that wraps impl and forwards calls of interface methods to calls to impl.</returns>
		public static TInterface Duck<TImpl>(TImpl impl)
		{
			if (impl == null)
				throw new ArgumentNullException(nameof(impl));

			var type = impl.GetType();
			Func<object, TInterface> del;
			if (duckCache.TryGetValue(type, out del))
				return del(impl);

			var makeDuckMethod = typeof(DuckInterface<,>)
				.MakeGenericType(typeof(TInterface), type)
				.GetMethod(nameof(DuckInterface<TInterface, TImpl>.Duck), BindingFlags.Public | BindingFlags.Static);


			DynamicMethod method = new DynamicMethod(Guid.NewGuid().ToString("N"), typeof(TInterface), new[] { typeof(object) }, true);
			var ilGen = method.GetILGenerator();
			ilGen.Emit(OpCodes.Ldarg_0);
			if (impl.GetType().IsValueType)
				ilGen.Emit(OpCodes.Unbox_Any, impl.GetType());
			else
				ilGen.Emit(OpCodes.Castclass, impl.GetType());
			ilGen.Emit(OpCodes.Call, makeDuckMethod);
			ilGen.Emit(OpCodes.Ret);

			del = (Func<object, TInterface>)method.CreateDelegate(typeof(Func<object, TInterface>));
			duckCache[type] = del;
			return del(impl);
		}
	}
	/// <summary>
	/// Allows to use all public methods required by TInterface on TImpl. The provided implementation type only needs to expose public methods matching the interface, it does NOT need to explicitly implement it.
	/// </summary>
	public static class DuckInterface<TInterface, TImpl>
		where TInterface : class
	{
		private static class Vars
		{
			public static AssemblyBuilder AssemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
				new AssemblyName("StaticInterfaces"),
				AssemblyBuilderAccess.Run
			);
			public static ModuleBuilder ModuleBuilder = AssemblyBuilder.DefineDynamicModule("MainModule");
		}

		private static Type implType;
		private static Func<TImpl, TInterface> factory;

		static DuckInterface()
		{
			if (!typeof(TInterface).IsInterface)
				throw new Exception($"The provided type {typeof(TInterface).FullName} is not an interface.");
			if (typeof(TInterface).GetCustomAttribute<StaticAttribute>() != null)
				throw new Exception($"The provided interface {typeof(TInterface).FullName} is marked with the {typeof(StaticAttribute).FullName} attribute. Duck typing does not work with static interfaces.");

			string typeName = typeof(TInterface).Name + "_" + typeof(TImpl).Name + "_" + Guid.NewGuid().ToString("N");
			TypeBuilder tb = Vars.ModuleBuilder.DefineType(
				typeName,
				TypeAttributes.Public |
				TypeAttributes.Class |
				TypeAttributes.AutoClass |
				TypeAttributes.AnsiClass |
				TypeAttributes.BeforeFieldInit |
				TypeAttributes.AutoLayout,
				null,
				new[] { typeof(TInterface) }
			);
			var field = tb.DefineField("_impl", typeof(TImpl), FieldAttributes.Private);
			foreach (MethodInfo method in typeof(TInterface).GetMethods())
			{
				Type[] methodParamTypes = method.GetParameters().Select(x => x.ParameterType).ToArray();
				MethodInfo targetMethod = Introspecter.GetImplementingMethod(method, typeof(TImpl), staticImplementation: false, throwOnError: true);
				if (targetMethod == null)
					throw new Exception($"Could not find required public method \"{method.Name}({string.Join(", ", methodParamTypes.Select(x => x.FullName).ToArray())})\" on implementation type \"{typeof(TImpl).FullName}\" of interface \"{typeof(TInterface).FullName}\"");

				MethodBuilder methodBuilder = tb.DefineMethod(
					method.Name,
					MethodAttributes.Public |
					MethodAttributes.HideBySig |
					MethodAttributes.NewSlot |
					MethodAttributes.Virtual |
					MethodAttributes.Final,
					method.ReturnType,
					methodParamTypes
				);
				GenericTypeParameterBuilder[] genericParamBuilders = null;
				if (method.IsGenericMethodDefinition)
				{
					var genericParams = method.GetGenericArguments();
					genericParamBuilders = methodBuilder.DefineGenericParameters(genericParams.Select(x => x.Name).ToArray());
					for (int i = 0; i < genericParamBuilders.Length; ++i)
					{
						genericParamBuilders[i].SetGenericParameterAttributes(genericParams[i].GenericParameterAttributes);

						var baseType = genericParams[i].GetGenericParameterConstraints().SingleOrDefault(x => x.IsClass);
						if (baseType != null)
							genericParamBuilders[i].SetBaseTypeConstraint(genericParams[i].BaseType);

						genericParamBuilders[i].SetInterfaceConstraints(genericParams[i].GetGenericParameterConstraints().Where(x => !x.IsClass).ToArray());
					}
				}

				ILGenerator ilGen = methodBuilder.GetILGenerator();
				ilGen.Emit(OpCodes.Ldarg_0);
				if (typeof(TImpl).IsValueType)
					ilGen.Emit(OpCodes.Ldflda, field);
				else
					ilGen.Emit(OpCodes.Ldfld, field);
				switch (methodParamTypes.Length)
				{
					case 0:
						break;
					case 1:
						ilGen.Emit(OpCodes.Ldarg_1);
						break;
					case 2:
						ilGen.Emit(OpCodes.Ldarg_1);
						ilGen.Emit(OpCodes.Ldarg_2);
						break;
					case 3:
						ilGen.Emit(OpCodes.Ldarg_1);
						ilGen.Emit(OpCodes.Ldarg_2);
						ilGen.Emit(OpCodes.Ldarg_3);
						break;
					default:
						ilGen.Emit(OpCodes.Ldarg_1);
						ilGen.Emit(OpCodes.Ldarg_2);
						ilGen.Emit(OpCodes.Ldarg_3);
						int numParams = methodParamTypes.Length;
						if (numParams > byte.MaxValue)
							throw new Exception($"Only methods with up to {byte.MaxValue} parameters are allowed.");

						for (int i = 4; i < numParams; ++i)
							ilGen.Emit(OpCodes.Ldarg, (byte)numParams);
						break;
				}
				if (targetMethod.IsGenericMethodDefinition)
					targetMethod.MakeGenericMethod(genericParamBuilders);
				if(typeof(TImpl).IsValueType)
					ilGen.Emit(OpCodes.Call, targetMethod);
				else
					ilGen.Emit(OpCodes.Callvirt, targetMethod);

				ilGen.Emit(OpCodes.Ret);
			}

			var ctor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(TImpl) });
			ILGenerator ctorILGen = ctor.GetILGenerator();
			ctorILGen.Emit(OpCodes.Ldarg_0);
			ctorILGen.Emit(OpCodes.Ldarg_1);
			ctorILGen.Emit(OpCodes.Stfld, field);
			ctorILGen.Emit(OpCodes.Ret);

			var makeMethod = tb.DefineMethod(
				"Make",
				MethodAttributes.Public | MethodAttributes.Static,
				typeof(TInterface),
				new[] { typeof(TImpl) });
			var makeILGen = makeMethod.GetILGenerator();
			makeILGen.Emit(OpCodes.Ldarg_0);
			makeILGen.Emit(OpCodes.Newobj, ctor);
			makeILGen.Emit(OpCodes.Ret);

			implType = tb.CreateType();
			factory = (Func<TImpl, TInterface>)implType.GetMethod("Make", BindingFlags.Public | BindingFlags.Static).CreateDelegate(typeof(Func<TImpl, TInterface>));
		}

		/// <summary>
		/// Creates an instance of a wrapper that wraps impl and forwards calls of interface methods to calls to impl.
		/// </summary>
		/// <param name="impl">The implementation that should be wrapped in an interface.</param>
		/// <returns>An instance of a wrapper that wraps impl and forwards calls of interface methods to calls to impl.</returns>
		public static TInterface Duck(TImpl impl)
		{
			//return (TInterface)Activator.CreateInstance(implType, impl);
			return factory(impl);
		}
	}
}
