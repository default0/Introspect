using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Introspect
{
	/// <summary>
	/// Inherit from this interface, providing the static interface you intend to implement as a type parameter, if you want the class to be used with the StaticInterface&lt;TInterface, TImpl&gt; class.
	/// </summary>
	/// <typeparam name="T">The static interface you intend to implement.</typeparam>
	public interface IStatic<T>
		where T : class
	{
	}
}
