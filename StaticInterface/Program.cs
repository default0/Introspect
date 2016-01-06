using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StaticInterface
{
	[Static]
	public interface IFoo
	{
		void Foo();
		int Bar(int x);
		void Baz(int x, string y);
	}

	public class A
	{
		public static void Foo()
		{
			Console.WriteLine("A.Foo");
		}
		public static int Bar(int x)
		{
			Console.WriteLine("A.Bar");
			return x;
		}
		public static void Baz(int x, string y)
		{
			Console.WriteLine("A.Baz");
		}
	}
	public class B
	{
		public static void Foo()
		{
			Console.WriteLine("B.Foo");
		}
		public static int Bar(int x)
		{
			Console.WriteLine("B.Bar");
			return x;
		}
		public static void Baz(int x, string y)
		{
			Console.WriteLine("B.Baz");
		}
	}

	public class C : IFoo
	{
		void IFoo.Foo() { }
		public void Baz(int x, string z) { }
		public int Bar(int x) { return x; }
	}

	class Program
	{
		static void Main(string[] args)
		{
			StaticInterface<IFoo, A>.Instance.Foo();
			StaticInterface<IFoo, A>.Instance.Bar(5);
			StaticInterface<IFoo, A>.Instance.Baz(5, "test");

			StaticInterface<IFoo, B>.Instance.Foo();
			StaticInterface<IFoo, B>.Instance.Bar(5);
			StaticInterface<IFoo, B>.Instance.Baz(5, "test");

			Console.ReadLine();
		}
	}
}
