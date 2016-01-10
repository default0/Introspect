using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Introspect.Test
{
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
		public TypeCode GetTypeCode()
		{
			Random rng = new Random();
			int tcVal = 0;
			while (rng.NextDouble() < 0.9)
				++tcVal;
			return (TypeCode)tcVal;
		}
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

		private static void Instance_X(object sender, EventArgs e)
		{
			Console.WriteLine("X Event Handler");
		}

	}
}
