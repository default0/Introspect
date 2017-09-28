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

		private static object[] empty = new object[0];

		private static MethodInfo isDuckImplMethod = typeof(Introspecter).GetMethod(nameof(isDuckImpl), BindingFlags.NonPublic | BindingFlags.Static);
		private static Dictionary<Tuple<Type, Type>, bool> isDuckCache = new Dictionary<Tuple<Type, Type>, bool>();

		/// <summary>
		/// True if the provided implementation type implements the provided interface type explicitly. If the provided interface type is a static interface, this method will automatically check whether the implementation type implements the interface statically.
		/// </summary>
		/// <typeparam name="TImpl">The implementation type that should implement the interface.</typeparam>
		/// <typeparam name="TInterface">The interface type that must be implemented.</typeparam>
		/// <returns>True if the implementation type explicitly implements the interface type, false otherwise.</returns>
		public static bool IsImplementation<TImpl, TInterface>()
			where TInterface : class
		{
			return isImplementationImpl<TImpl, TInterface>();
		}
		private static bool isImplementationImpl<TImpl, TInterface>()
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
		/// True if the provided implementation contains all methods defined in the provided interface type. The provided interface type may not be a static interface.
		/// </summary>
		/// <param name="impl">The object that must implement all methods of the interface type.</param>
		/// <typeparam name="TImpl">The base type of the implementation.</typeparam>
		/// <typeparam name="TInterface">The interface type that defines the methods that must be implemented.</typeparam>
		/// <returns>True if the implementation implements all methods of the interface type, false otherwise.</returns>
		public static bool IsDuck<TInterface, TImpl>(TImpl impl)
			where TInterface : class
		{
			// do not use typeof(TImpl) directly here, since impl may be a subclass
			// of TImpl.
			bool result;
			var tuple = new Tuple<Type, Type>(impl.GetType(), typeof(TInterface));
			if (isDuckCache.TryGetValue(tuple, out result))
				return result;

			if (impl == null)
				return false;
			if (typeof(TInterface).GetCustomAttribute<StaticAttribute>() != null)
				throw new Exception($"You cannot check for the implementation of a static interface on an object. Use the IsDuck overload that takes two type parameters instead.");

			result = (bool)isDuckImplMethod.MakeGenericMethod(impl.GetType(), typeof(TInterface)).Invoke(null, empty);
			isDuckCache[tuple] = result;
			return result;
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
			return isDuckImpl<TImpl, TInterface>();
		}
		private static bool isDuckImpl<TImpl, TInterface>()
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
				MethodInfo targetMethod = GetImplementingMethod(method, typeof(TImpl), staticImplementation: false, throwOnError: false);
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
				MethodInfo targetMethod = GetImplementingMethod(method, typeof(TImpl), staticImplementation: true, throwOnError: false);
				if (targetMethod == null)
					return false;
				if (targetMethod.GetParameters().Length > byte.MaxValue)
					return false;
			}
			return true;
		}

		internal static MethodInfo GetImplementingMethod(MethodInfo interfaceMethod, Type implementingType, bool staticImplementation, bool throwOnError)
		{
			Type[] methodParamTypes = interfaceMethod.GetParameters().Select(x => x.ParameterType).ToArray();
			List<MethodInfo> targetMethods = new List<MethodInfo>();
			MethodInfo[] candidateMethods = implementingType.GetMethods(
				(staticImplementation ? 
					BindingFlags.Public | BindingFlags.Static :
					BindingFlags.Public | BindingFlags.Instance) |
				BindingFlags.FlattenHierarchy
			);
			foreach (MethodInfo candidate in candidateMethods)
			{
				if (candidate.Name != interfaceMethod.Name)
					continue;

				if (!checkTypeEquivalency(candidate.ReturnType, interfaceMethod.ReturnType))
					continue;

				ParameterInfo[] candidateParams = candidate.GetParameters();
				if (candidateParams.Length != methodParamTypes.Length)
					continue;

				bool compatibleTypes = true;
				for (int i = 0; i < methodParamTypes.Length; ++i)
				{
					if (!checkTypeEquivalency(methodParamTypes[i], candidateParams[i].ParameterType))
					{
						compatibleTypes = false;
						break;
					}
				}
				if (!compatibleTypes)
					continue;

				if (candidate.DeclaringType == implementingType) // perfect inheritance score - we can just return him, there will be no better candidates.
					return candidate;

				targetMethods.Add(candidate);
			}
			var declaringTypes = targetMethods.ToDictionary(x => x.DeclaringType);
			// we can start from the base type because if we would have had a candidate on the implementingType itself we would have already returned it.
			var curType = implementingType.BaseType;
			while(curType != null)
			{
				MethodInfo bestCandidate;
				if (declaringTypes.TryGetValue(curType, out bestCandidate))
					return bestCandidate;

				curType = curType.BaseType;
			}
			if (throwOnError)
				throw new Exception($"Could not find a suitable method to implement the interface method {interfaceMethod.ToString()} of interface {interfaceMethod.DeclaringType.FullName}.");
			else
				return null;
		}

		private static bool checkTypeEquivalency(Type left, Type right)
		{
			if (left != right)
			{
				if (left.ContainsGenericParameters && right.ContainsGenericParameters)
				{
					if (left.IsGenericParameter && right.IsGenericParameter)
					{
						if (left.GenericParameterAttributes != right.GenericParameterAttributes)
							return false;
						if (left.GenericParameterPosition != right.GenericParameterPosition)
							return false;
					}
					else
					{
						if (left.GetGenericTypeDefinition() != right.GetGenericTypeDefinition())
							return false;

						var leftArgs = left.GetGenericArguments();
						var rightArgs = right.GetGenericArguments();
						if (leftArgs.Length != rightArgs.Length)
							return false;

						for (int i = 0; i < leftArgs.Length; ++i)
						{
							if (!checkTypeEquivalency(leftArgs[i], rightArgs[i]))
								return false;
						}
					}
				}
				else
				{
					return false;
				}
			}
			return true;
		}
	}
}
