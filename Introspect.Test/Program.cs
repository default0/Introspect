using Introspect;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Introspect.Test
{
	class DuckOfStruct : IDuck
	{
		private StructDuck duck;

		public void Flap()
		{
			duck.Flap();
		}

		public void Quack()
		{
			duck.Quack();
		}
	}

	[Static]
	public interface IFoo
	{
		// static interface should disallow indexer
		int Prop { get; }
		event EventHandler<EventArgs> X;

		void Baz(int x, string y);
		T GenericFoo<T>(T x);
	}

	[Static]
	public interface IParseable<T>
	{
		bool TryParse(string str, out T result);
	}

	[Static]
	public interface IInheritanceTest
	{
		void M();
		void N();
	}

	[Static]
	public interface ITestContainsGenericParams
	{
		List<T> MakeList<T>(HashSet<T> hashSet) where T : InheritanceBase;
	}

	public class TestContainsGenericParams : IStatic<ITestContainsGenericParams>
	{
		public static List<T> MakeList<T>(HashSet<T> hashSet) where T : InheritanceBase
		{
			Console.WriteLine("HashSet-Count " + hashSet.Count.ToString());
			return new List<T>();
		}
	}

	public class InheritanceBase : IStatic<IInheritanceTest>
	{
		public static void M()
		{
			Console.WriteLine("InheritanceBase.M was called.");
		}
		public static void N()
		{
			Console.WriteLine("InheritanceBase.N was called.");
		}
	}
	public class InheritanceSub : InheritanceBase
	{
		public static new void M()
		{
			Console.WriteLine("InheritanceSub.M was called.");
		}
	}

	public class FooStatic : IStatic<IFoo>
	{
		public static int Prop => 5;
		public static event EventHandler<EventArgs> X;
		public static void Baz(int x, string yasas) { Console.WriteLine("FooStatic.Baz, x = " + x + ", y = " + yasas); }
		public static TLel GenericFoo<TLel>(TLel x) => x;

		public static void TriggerX()
		{
			if (X != null)
				X(null, new EventArgs());
		}
	}

	public interface ITypeCoded
	{
		TypeCode GetTypeCode();
	}
	public class MyTC
	{
	}
	public class MySubTC
	{
		private static Random rng = new Random();

		public TypeCode GetTypeCode()
		{
			return (TypeCode)rng.Next(0, 2);
		}
	}

	public interface IDuck
	{
		void Quack();
		void Flap();
	}
	public struct StructDuck
	{
		public void Quack() => Console.WriteLine("Struct Quack");
		public void Flap() => Console.WriteLine("Struct Flap");
	}
	public class NotADuck
	{
		public void Quack() { }
	}
	public class Duck
	{
		public void Quack() => Console.WriteLine("Class Quack");
		public void Flap() => Console.WriteLine("Class Flap");
	}
	public class Program
	{
		public int x;

		public Program(int x)
		{
			this.x = x;
		}

		public TypeCode GetTypeCode()
		{
			return x.GetTypeCode();
		}

		public static void M<T>(T x)
		{
			B(x);
		}
		public static void B<T>(T x)
		{

		}

		public static void Main(string[] args)
		{
			// Expected Output:
			// Duck is a duck of IDuck
			// NotADuck is not a duck of IDuck
			Console.WriteLine($"Duck is {(Introspecter.IsDuck<IDuck, Duck>(new Duck()) ? "" : "not")} a duck of IDuck");
			Console.WriteLine($"NotADuck is {(Introspecter.IsDuck<IDuck, NotADuck>(new NotADuck()) ? "" : "not")} a duck of IDuck");



			var structDuck = DuckInterface<IDuck>.Duck(new StructDuck());
			var classDuck = DuckInterface<IDuck>.Duck(new Duck());

			structDuck.Quack();
			structDuck.Flap();
			classDuck.Quack();
			classDuck.Flap();

			// Expected Output:
			// InheritanceBase.M was called
			// InheritanceBase.N was called
			// InheritanceSub.M was called
			// InheritanceBase.N was called
			TestInheritance<InheritanceBase>();
			TestInheritance<InheritanceSub>();

			// Expected Output: HashSet-Count 0
			StaticInterface<ITestContainsGenericParams, TestContainsGenericParams>.Impl.MakeList<InheritanceSub>(new HashSet<InheritanceSub>());

			const int epochs = 100;
			const int iterations = 100000;
			Stopwatch sw = Stopwatch.StartNew();
			double min1 = double.MaxValue;
			double min2 = double.MaxValue;
			float f = (float)new Random().NextDouble();
			TypeCode tc = TypeCode.Boolean;
			for (int j = 0; j < epochs; ++j)
			{
				sw.Restart();
				for (int i = 0; i < iterations; ++i)
					tc = GetTypeCode(new MySubTC());
				sw.Stop();

				if (sw.Elapsed.TotalMilliseconds < min1)
					min1 = sw.Elapsed.TotalMilliseconds;

				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
				Console.Title = $"Epoch {j + 1}/{epochs}, Test 1";
			}
			for(int j = 0; j < epochs; ++j)
			{
				sw.Restart();
				for (int i = 0; i < iterations; ++i)
					tc = new MySubTC().GetTypeCode();
				sw.Stop();

				if (sw.Elapsed.TotalMilliseconds < min2)
					min2 = sw.Elapsed.TotalMilliseconds;

				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
				Console.Title = $"Epoch {j + 1}/{epochs}, Test 2";
			}

			Console.WriteLine("Generic = " + min1);
			Console.WriteLine("Non-Generic = " + min2);
			Console.WriteLine("", tc.ToString());

			Console.ReadLine();
		}
		
		
		public static TypeCode GetTypeCode<T>(T typecoded)
		{
			return DuckInterface<ITypeCoded>.Duck(typecoded).GetTypeCode();
		}
		public static bool TryParse<T>(string str, out T result)
		{
			if (!Introspecter.IsDuck<T, IParseable<T>>())
				throw new Exception($"Type {typeof(T).FullName} does not implement all methods required by {typeof(IParseable<T>).FullName}.");

			return StaticDuckInterface<IParseable<T>, T>.Impl.TryParse(str, out result);
		}
		public static bool TryParseUnchecked<T>(string str, out T result)
		{
			return StaticDuckInterface<IParseable<T>, T>.Impl.TryParse(str, out result);
		}

		public static void TestInheritance<T>() where T : IStatic<IInheritanceTest>
		{
			StaticInterface<IInheritanceTest, T>.Impl.M();
			StaticInterface<IInheritanceTest, T>.Impl.N();
			
		}

		private static void Instance_X(object sender, EventArgs e)
		{
			Console.WriteLine("X Event Handler");
		}

	}
}
