using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Introspect
{
	/// <summary>
	/// Class that holds methods useful for implementing Design by Introspection.
	/// </summary>
	public static class Introspecter
	{
		private static class ImplementationMemoizer<T1, T2>
		{
			public static bool? result;
		}
		private static class DuckMemoizer<T1, T2>
		{
			public static bool? result;
		}

		/// <summary>
		/// True if the provided implementation type implements the provided interface type explicitly. If the provided interface type is a static interface, this method will automatically check whether the implementation type implements the interface statically.
		/// </summary>
		/// <typeparam name="TImpl">The implementation type that should implement the interface.</typeparam>
		/// <typeparam name="TInterface">The interface type that must be implemented.</typeparam>
		/// <returns>True if the implementation type explicitly implements the interface type, false otherwise.</returns>
		public static bool IsImplementation<TImpl, TInterface>()
			where TInterface : class
		{
			if (ImplementationMemoizer<TImpl, TInterface>.result.HasValue)
				return ImplementationMemoizer<TImpl, TInterface>.result.Value;

			if (!typeof(TInterface).IsInterface)
				throw new Exception($"The provided type {typeof(TInterface).FullName} is not an interface.");

			if (typeof(TInterface).GetCustomAttribute<StaticAttribute>() != null)
			{
				bool result = isStaticImplementation<TImpl, TInterface>(checkDuck: false);
				ImplementationMemoizer<TImpl, TInterface>.result = result;
				return result;
			}

			bool r = typeof(TInterface).IsAssignableFrom(typeof(TImpl));
			ImplementationMemoizer<TImpl, TInterface>.result = r;
			return r;
		}
		
		/// <summary>
		/// True if the provided implementation type contains all methods defined in the provided interface type. If the provided interface type is a static interface, this method will automatically check whether the implementation type contains all specified public static methods.
		/// </summary>
		/// <typeparam name="TImpl">The implementation type that should implement all methods of the interface.</typeparam>
		/// <typeparam name="TInterface">The interface type that defines the methods that must be implemented.</typeparam>
		/// <returns>True if the implementation type implements all methods of the interface type, false otherwise.</returns>
		public static bool IsDuck<TImpl, TInterface>()
			where TInterface : class
		{
			if (DuckMemoizer<TImpl, TInterface>.result.HasValue)
				return DuckMemoizer<TImpl, TInterface>.result.Value;

			if (!typeof(TInterface).IsInterface)
				throw new Exception($"The provided type {typeof(TInterface).FullName} is not an interface.");

			if (typeof(TInterface).GetCustomAttribute<StaticAttribute>() != null)
			{
				bool result = isStaticImplementation<TImpl, TInterface>(checkDuck: true);
				DuckMemoizer<TImpl, TInterface>.result = result;
				return result;
			}

			foreach (MethodInfo method in typeof(TInterface).GetMethods())
			{
				MethodInfo targetMethod = GetImplementingMethod(method, typeof(TImpl), staticImplementation: false);
				if (targetMethod == null)
				{
					bool result = false;
					DuckMemoizer<TImpl, TInterface>.result = result;
					return result;
				}
			}

			bool r = true;
			DuckMemoizer<TImpl, TInterface>.result = r;
			return r;
		}

		private static bool isStaticImplementation<TImpl, TInterface>(bool checkDuck)
			where TInterface : class
		{
			if (!checkDuck && !typeof(IStatic<TInterface>).IsAssignableFrom(typeof(TImpl)))
				return false;

			foreach(MethodInfo method in typeof(TInterface).GetMethods())
			{
				MethodInfo targetMethod = GetImplementingMethod(method, typeof(TImpl), staticImplementation: true);
				if (targetMethod == null)
					return false;
				if (targetMethod.GetParameters().Length > byte.MaxValue)
					return false;
			}
			return true;
		}

		internal static MethodInfo GetImplementingMethod(MethodInfo interfaceMethod, Type implementingType, bool staticImplementation)
		{
			Type[] methodParamTypes = interfaceMethod.GetParameters().Select(x => x.ParameterType).ToArray();
			MethodInfo targetMethod = null;
			MethodInfo[] candidateMethods = implementingType.GetMethods(
				staticImplementation ? 
					BindingFlags.Public | BindingFlags.Static :
					BindingFlags.Public | BindingFlags.Instance
			);
			foreach (MethodInfo candidate in candidateMethods)
			{
				if (candidate.Name != interfaceMethod.Name)
					continue;
				if (candidate.ReturnType != interfaceMethod.ReturnType)
				{
					if (candidate.ReturnType.IsGenericParameter && interfaceMethod.ReturnType.IsGenericParameter)
					{
						if (candidate.ReturnType.GenericParameterAttributes != interfaceMethod.ReturnType.GenericParameterAttributes)
							continue;
						if (candidate.ReturnType.GenericParameterPosition != interfaceMethod.ReturnType.GenericParameterPosition)
							continue;
					}
					else
					{
						continue;
					}
				}

				ParameterInfo[] candidateParams = candidate.GetParameters();
				if (candidateParams.Length != methodParamTypes.Length)
					continue;

				bool compatibleTypes = true;
				for (int i = 0; i < methodParamTypes.Length; ++i)
				{
					if (methodParamTypes[i] != candidateParams[i].ParameterType)
					{
						if (candidateParams[i].ParameterType.IsGenericParameter && methodParamTypes[i].IsGenericParameter)
						{
							if (candidateParams[i].ParameterType.GenericParameterAttributes != methodParamTypes[i].GenericParameterAttributes)
								compatibleTypes = false;
							if (candidateParams[i].ParameterType.GenericParameterPosition != methodParamTypes[i].GenericParameterPosition)
								compatibleTypes = false;
						}
						else
						{
							compatibleTypes = false;
						}
					}
					if (!compatibleTypes)
						break;
				}
				if (!compatibleTypes)
					continue;

				targetMethod = candidate;
				break;
			}
			return targetMethod;
		}
	}
}
