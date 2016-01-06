using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace StaticInterface
{
	/// <summary>
	/// Marks an interface as a static interface.
	/// </summary>
	[AttributeUsage(AttributeTargets.Interface)]
	public class StaticAttribute : Attribute { }
	/// <summary>
	/// Allows to use all public static methods required by <see cref="TInterface"/> on <see cref="TImpl"/>. The provided interface must be marked with the <see cref="StaticAttribute"/>.
	/// </summary>
	public class StaticInterface<TInterface, TImpl>
		where TInterface : class
	{
		private static class Vars
		{
			public static AssemblyBuilder AssemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
				new AssemblyName("StaticInterfaces"),
				AssemblyBuilderAccess.Run
			);
			public static ModuleBuilder ModuleBuilder = AssemblyBuilder.DefineDynamicModule("MainModule");

			static Vars()
			{
				Console.WriteLine("Vars cctor");
			}
		}

		/// <summary>
		/// Provides access to the public static method of <see cref="TImpl"/>.
		/// </summary>
		public static TInterface Instance { get; private set; }

		static StaticInterface()
		{
			Console.WriteLine("StaticInterface cctor");
			if (!typeof(TInterface).IsInterface)
				throw new Exception($"The provided type {typeof(TInterface).FullName} is not an interface.");
			if (typeof(TInterface).GetCustomAttribute<StaticAttribute>() == null)
				throw new Exception($"The provided interface {typeof(TInterface).FullName} is not marked with the {typeof(StaticAttribute).FullName} attribute.");

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
				new [] { typeof(TInterface) }
			);
			foreach(MethodInfo method in typeof(TInterface).GetMethods())
			{
				Type[] methodParamTypes = method.GetParameters().Select(x => x.ParameterType).ToArray();
				MethodInfo targetMethod = null;
				MethodInfo[] candidateMethods = typeof(TImpl).GetMethods(BindingFlags.Public | BindingFlags.Static);
				foreach(MethodInfo candidate in candidateMethods)
				{
					if (candidate.Name != method.Name)
						continue;
					if (candidate.ReturnType != method.ReturnType)
						continue;

					ParameterInfo[] candidateParams = candidate.GetParameters();
					if (candidateParams.Length != methodParamTypes.Length)
						continue;

					bool compatibleTypes = true;
					for(int i = 0; i < methodParamTypes.Length; ++i)
					{
						if(methodParamTypes[i] != candidateParams[i].ParameterType)
						{
							compatibleTypes = false;
							break;
						}
					}
					if (!compatibleTypes)
						continue;

					targetMethod = candidate;
					break;
				}
				if (targetMethod == null)
					throw new Exception($"Could not find required public static method \"{method.Name}({string.Join(", ", methodParamTypes.Select(x => x.FullName).ToArray())})\" on implementation type \"{typeof(TImpl).FullName}\" of interface \"{typeof(TInterface).FullName}\"");

				// public hidebysig newslot virtual final
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

				ILGenerator ilGen = methodBuilder.GetILGenerator();
				switch (methodParamTypes.Length)
				{
					case 0:
						break;
					case 1:
						ilGen.Emit(OpCodes.Ldarg_0);
						break;
					case 2:
						ilGen.Emit(OpCodes.Ldarg_0);
						ilGen.Emit(OpCodes.Ldarg_1);
						break;
					case 3:
						ilGen.Emit(OpCodes.Ldarg_0);
						ilGen.Emit(OpCodes.Ldarg_1);
						ilGen.Emit(OpCodes.Ldarg_2);
						break;
					case 4:
						ilGen.Emit(OpCodes.Ldarg_0);
						ilGen.Emit(OpCodes.Ldarg_1);
						ilGen.Emit(OpCodes.Ldarg_2);
						ilGen.Emit(OpCodes.Ldarg_3);
						break;
					default:
						ilGen.Emit(OpCodes.Ldarg_0);
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
				ilGen.Emit(OpCodes.Call, targetMethod);
				ilGen.Emit(OpCodes.Ret);
			}
			Instance = (TInterface)Activator.CreateInstance(tb.CreateType());
		}
	}
}
