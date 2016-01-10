using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Introspect
{
	/// <summary>
	/// Allows to use all public static methods required by TInterface on TImpl the provided interface must be marked with the <see cref="StaticAttribute"/> and the provided implementation must implement the interface <see cref="IStatic{TInterface}"/>
	/// </summary>
	public static class StaticInterface<TInterface, TImpl>
		where TInterface : class
		where TImpl : IStatic<TInterface>
	{
		/// <summary>
		/// Provides access to the public static methods of TImpl.
		/// </summary>
		public static TInterface Impl => StaticDuckInterface<TInterface, TImpl>.Impl;
	}
}
