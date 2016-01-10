using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using Introspect.Analyzer;

namespace Introspect.Analyzer.Test
{
	[TestClass]
	public class UnitTest : CodeFixVerifier
	{
		[TestMethod]
		public void TestCannotImplementStaticInterfaceDirectly()
		{
			var test = @"
	using StaticInterface;
	
	namespace Test
	{
		[Static]
		public interface IFoo { }
		public interface IRegular { }

		public class Foo : IFoo, IRegular { }
	}";
			var expected = new DiagnosticResult
			{
				Id = "SI0001",
				Message = String.Format("You cannot implement static interface '{0}' directly. Implement the interface IStatic<{0}> instead.", "IFoo"),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 10, 16)
				}
			};

			VerifyCSharpDiagnostic(test, expected);
		}

		[TestMethod]
		public void TestCanOnlyUseStaticInterfaceTypeParamForIStatic()
		{
			var test = @"
	using StaticInterface;

	namespace Test
	{
		public delegate void MyDelegate();

		[Static]
		public interface IMyStatic { }
		public interface IMyNonStatic { }

		public class ErrorClass : IStatic<IMyNonStatic> { }
		public class ErrorClass2 : IStatic<ErrorClass> { }
		public class ErrorClass3 : IStatic<int[]> { }
		public class ErrorClass4 : IStatic<MyDelegate> { }

		public interface IError : IStatic<IMyNonStatic> { }
		public interface IError2 : IStatic<ErrorClass> { }
		public interface IError3 : IStatic<int[]> { }
		public interface IError4 : IStatic<MyDelegate> { }
		
		public interface ICorrect : IStatic<IMyStatic> { }
		public interface ICorrect : IStatic<ErrorType> { }
		public class CorrectClass : IStatic<IMyStatic> { }
		public class CorrectClass : IStatic<ErrorType> { }
	}
";
			var expected = new DiagnosticResult
			{
				Id = "SI0002",
				Message = String.Format("The type parameter of the IStatic<T> interface must be an interface marked with the StaticAttribute."),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 12, 16)
				}
			};
			var expected2 = expected;
			expected2.Locations = new[]
			{
				new DiagnosticResultLocation("Test0.cs", 13, 16)
			};
			var expected3 = expected;
			expected3.Locations = new[]
			{
				new DiagnosticResultLocation("Test0.cs", 14, 16)
			};
			var expected4 = expected;
			expected4.Locations = new[]
			{
				new DiagnosticResultLocation("Test0.cs", 15, 16)
			};
			var expected5 = expected;
			expected5.Locations = new[]
			{
				new DiagnosticResultLocation("Test0.cs", 17, 20)
			};
			var expected6 = expected;
			expected6.Locations = new[]
			{
				new DiagnosticResultLocation("Test0.cs", 18, 20)
			};
			var expected7 = expected;
			expected7.Locations = new[]
			{
				new DiagnosticResultLocation("Test0.cs", 19, 20)
			};
			var expected8 = expected;
			expected8.Locations = new[]
			{
				new DiagnosticResultLocation("Test0.cs", 20, 20)
			};

			VerifyCSharpDiagnostic(test, expected, expected2, expected3, expected4, expected5, expected6, expected7, expected8);
		}

		[TestMethod]
		public void TestStaticInterfaceCannotInheritNonStaticInterface()
		{
			var test = @"
	using StaticInterface;
	
	namespace Test
	{
		public interface IRegular { }

		[Static]
		public interface IFoo : IRegular { }

		// correct cases
		[Static]
		public interface IBar : IFoo { }
		public interface IOther : IRegular { }
		public interface IYetAnother : ErrorType { }
	}";
			var expected = new DiagnosticResult
			{
				Id = "SI0004",
				Message = String.Format("Static interface '{0}' cannot inherit from non-static interface '{1}'.", "IFoo", "IRegular"),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 9, 20)
				}
			};

			VerifyCSharpDiagnostic(test, expected);
		}

		[TestMethod]
		public void TestStaticInterfaceCannotContainIndexer()
		{
			var test = @"
	using StaticInterface;
	
	namespace Test
	{
		public interface IRegular
		{
			int this[int index] { get; }
		}

		[Static]
		public interface IFoo
		{
			int this[int index] { get; }
		}
	}";
			var expected = new DiagnosticResult
			{
				Id = "SI0005",
				Message = String.Format("Static interface '{0}' cannot contain an indexer.", "IFoo"),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 14, 8)
				}
			};

			VerifyCSharpDiagnostic(test, expected);
		}

		[TestMethod]
		public void TestClassImplementsStaticInterface()
		{
			var test = @"
	using System;
	using StaticInterface;
	
	namespace Test
	{
		[Static]
		public interface IFoo
		{
			void M(int x, string y);
			T GenericM<T>(T x);
			event EventHandler<EventArgs> X;
			int Prop { get; }
		}

		public class FooBadImpl : IStatic<IFoo>
		{
			public static void M(string x, int y) { }
			public static T GenericM<T>(T x) { } // this is correctly implemented.
			public static event EventHandler<ResolveEventArgs> X;
			public static long Prop => 0L;
		}

		public class FooCorrectImpl : IStatic<IFoo>
		{
			public static void M(int x, string y) { }
			public static T GenericM<T>(T x) { return x; }
			public static event EventHandler<EventArgs> X;
			public static int Prop => 3;
		}
	}";
			var expected = new DiagnosticResult
			{
				Id = "SI0003",
				Message = String.Format("'{0}' does not implement static interface member {1}. The implementing member must be public and static.", "FooBadImpl", "Test.IFoo.M(int, string)"),
				Severity = DiagnosticSeverity.Error,
				Locations = new[]
				{
					new DiagnosticResultLocation("Test0.cs", 16, 16)
				}
			};
			var expected2 = expected;
			expected2.Message = String.Format("'{0}' does not implement static interface member {1}. The implementing member must be public and static.", "FooBadImpl", "Test.IFoo.X");
			var expected3 = expected;
			expected3.Message = String.Format("'{0}' does not implement static interface member {1}. The implementing member must be public and static.", "FooBadImpl", "Test.IFoo.Prop");

			VerifyCSharpDiagnostic(test, expected, expected2, expected3);
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
		{
			return new StaticInterfaceAnalyzerAnalyzer();
		}
	}
}