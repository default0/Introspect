using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Introspect
{
	/// <summary>
	/// Marks an interface as a static interface.
	/// </summary>
	[AttributeUsage(AttributeTargets.Interface)]
	public class StaticAttribute : Attribute
	{
	}
}
