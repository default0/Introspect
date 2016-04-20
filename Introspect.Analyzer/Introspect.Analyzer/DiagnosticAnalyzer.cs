using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Introspect.Analyzer
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class StaticInterfaceAnalyzerAnalyzer : DiagnosticAnalyzer
	{
		private static DiagnosticDescriptor CannotImplementStaticInterfaceRule = new DiagnosticDescriptor(
			"SI0001",
			"Cannot implement Static Interface",
			"You cannot implement static interface '{0}' directly. Implement the interface IStatic<{0}> instead.",
			"Semantics",
			DiagnosticSeverity.Error,
			isEnabledByDefault: true,
			description: "Description goes here"
		);
		private static DiagnosticDescriptor IStaticTypeParameterRule = new DiagnosticDescriptor(
			"SI0002",
			"Must use Static Interface as type parameter for IStatic interface",
			"The type parameter of the IStatic<T> interface must be an interface marked with the StaticAttribute.",
			"Semantics",
			DiagnosticSeverity.Error,
			isEnabledByDefault: true,
			description: "Description goes here"
		);
		private static DiagnosticDescriptor DoesntImplementStaticMember = new DiagnosticDescriptor(
			"SI0003",
			"Doesn't implement member",
			"'{0}' does not implement static interface member {1}. The implementing member must be public and static.",
			"Semantics",
			DiagnosticSeverity.Error,
			isEnabledByDefault: true,
			description: "Description goes here"
		);
		private static DiagnosticDescriptor StaticInterfaceCannotInheritFromNonStaticInterface = new DiagnosticDescriptor(
			"SI0004",
			"Cannot inherit non-static interface",
			"Static interface '{0}' cannot inherit from non-static interface '{1}'.",
			"Semantics",
			DiagnosticSeverity.Error,
			isEnabledByDefault: true,
			description: "Description goes here"
		);
		private static DiagnosticDescriptor StaticInterfaceCannotContainIndexer = new DiagnosticDescriptor(
			"SI0005",
			"Static interface cannot contain indexer",
			"Static interface '{0}' cannot contain an indexer.",
			"Semantics",
			DiagnosticSeverity.Error,
			isEnabledByDefault: true,
			description: "Description goes here"
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
		{
			get
			{
				return ImmutableArray.Create(
					IStaticTypeParameterRule,
					CannotImplementStaticInterfaceRule,
					DoesntImplementStaticMember,
					StaticInterfaceCannotInheritFromNonStaticInterface,
					StaticInterfaceCannotContainIndexer
				);
			}
		}

		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSymbolAction(ctx =>
			{
				bool areInternalsVisible = false;
				AttributeData[] internalsVisibleToAttrs = ctx.Compilation.Assembly.GetAttributes().Where(x => x.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Runtime.CompilerServices.InternalsVisibleTo").ToArray();
				foreach(AttributeData data in internalsVisibleToAttrs)
				{
					if(data.ConstructorArguments.Length > 0 &&
					   data.ConstructorArguments[0].Kind == TypedConstantKind.Primitive &&
					   data.ConstructorArguments[0].Value is string &&
					   ((string)data.ConstructorArguments[0].Value) == "StaticInterfaces"
					)
					{
						areInternalsVisible = true;
						break;
					}
				}
				foreach (Diagnostic diagnostic in analyzeSymbol(ctx.Symbol, areInternalsVisible))
					ctx.ReportDiagnostic(diagnostic);

			}, SymbolKind.NamedType);
		}

		private static IEnumerable<Diagnostic> analyzeSymbol(ISymbol symbol, bool areInternalsVisible)
		{
			List<Diagnostic> diagnostics = new List<Diagnostic>();
			if (symbol is ITypeSymbol)
			{
				foreach (ISymbol member in ((ITypeSymbol)symbol).GetMembers())
					diagnostics.AddRange(analyzeSymbol(member, areInternalsVisible));
			}
			else if (symbol is INamespaceSymbol)
			{
				foreach (ISymbol member in ((INamespaceSymbol)symbol).GetMembers())
					diagnostics.AddRange(analyzeSymbol(member, areInternalsVisible));
			}

			var namedTypeSymbol = symbol as INamedTypeSymbol;
			if (namedTypeSymbol == null)
				return diagnostics;

			if(isStaticInterface(namedTypeSymbol))
			{
				foreach(INamedTypeSymbol nonStaticInterface in namedTypeSymbol.Interfaces.Where(x => !isStaticInterface(x)))
				{
					var d = Diagnostic.Create(StaticInterfaceCannotInheritFromNonStaticInterface, namedTypeSymbol.Locations[0], namedTypeSymbol.Name, nonStaticInterface.Name);
					diagnostics.Add(d);
				}
				foreach(ISymbol member in namedTypeSymbol.GetMembers())
				{
					if(member.Kind == SymbolKind.Property)
					{
						IPropertySymbol property = (IPropertySymbol)member;
						if(property.IsIndexer)
							diagnostics.Add(Diagnostic.Create(StaticInterfaceCannotContainIndexer, member.Locations[0], namedTypeSymbol.Name));
					}
				}
				return diagnostics;
			}

			var badImplementedStaticInterfaces = namedTypeSymbol.Interfaces.Where(x => isStaticInterface(x)).ToArray();
			foreach (INamedTypeSymbol badImplementedStaticInterface in badImplementedStaticInterfaces)
			{
				var diagnostic = Diagnostic.Create(CannotImplementStaticInterfaceRule, namedTypeSymbol.Locations[0], badImplementedStaticInterface.Name);
				diagnostics.Add(diagnostic);
			}

			var implementedStaticInterfaces = namedTypeSymbol.Interfaces.Where(x => isStaticInterfaceImpl(x)).ToArray();
			foreach (INamedTypeSymbol implementedStaticInterface in implementedStaticInterfaces)
			{
				if (isStaticInterface(implementedStaticInterface.TypeArguments[0] as INamedTypeSymbol))
				{
					diagnostics.AddRange(checkStaticInterfaceImplementation(namedTypeSymbol, implementedStaticInterface.TypeArguments[0]));
					continue;
				}
				
				var diagnostic2 = Diagnostic.Create(IStaticTypeParameterRule, namedTypeSymbol.Locations[0]);
				diagnostics.Add(diagnostic2);
			}

			return diagnostics;
		}

		private static IEnumerable<Diagnostic> checkStaticInterfaceImplementation(INamedTypeSymbol implementingType, ITypeSymbol implementedInterface)
		{
			List<Diagnostic> diagnostics = new List<Diagnostic>();

			foreach(ISymbol symbol in implementedInterface.GetMembers())
			{
				if (symbol.Kind == SymbolKind.Property && ((IPropertySymbol)symbol).IsIndexer)
					continue;
				if (symbol.Kind == SymbolKind.Method && ((IMethodSymbol)symbol).MethodKind != MethodKind.Ordinary)
					continue;

				var possibleImplementations = implementingType.GetMembers(symbol.Name).Where(
					x => x.Kind == symbol.Kind &&
					x.DeclaredAccessibility == Accessibility.Public &&
					x.IsStatic
				).ToArray();
				if(possibleImplementations.Length == 0)
				{
					diagnostics.Add(Diagnostic.Create(DoesntImplementStaticMember, implementingType.Locations[0], implementingType.Name, symbol.ToDisplayString()));
					continue;
				}
				switch (symbol.Kind)
				{
					case SymbolKind.Event:
						if(!possibleImplementations.Cast<IEventSymbol>().Any(x => x.Type.Equals(((IEventSymbol)symbol).Type)))
							diagnostics.Add(Diagnostic.Create(DoesntImplementStaticMember, implementingType.Locations[0], implementingType.Name, symbol.ToDisplayString()));
						break;
					case SymbolKind.Property:
						if(!possibleImplementations.Cast<IPropertySymbol>().Any(x => x.Type.Equals(((IPropertySymbol)symbol).Type)))
							diagnostics.Add(Diagnostic.Create(DoesntImplementStaticMember, implementingType.Locations[0], implementingType.Name, symbol.ToDisplayString()));
						break;
					case SymbolKind.Method:
						if(!possibleImplementations.Cast<IMethodSymbol>().Any(x => isValidMethodImplementation(x, (IMethodSymbol)symbol)))
							diagnostics.Add(Diagnostic.Create(DoesntImplementStaticMember, implementingType.Locations[0], implementingType.Name, symbol.ToDisplayString()));
						break;
				}
			}

			return diagnostics;
		}

		private static bool isValidMethodImplementation(IMethodSymbol implementation, IMethodSymbol implemented)
		{
			if (implementation.Name != implemented.Name)
				return false;
			if (implementation.Arity != implemented.Arity)
				return false;
			for(int i = 0; i < implementation.TypeParameters.Length; ++i)
			{
				if (!checkMatchingTypeParameters(implementation.TypeParameters[i], implemented.TypeParameters[i]))
					return false;
			}

			if (implementation.ReturnType.TypeKind != implemented.ReturnType.TypeKind)
				return false;
			if (implementation.ReturnType.TypeKind == TypeKind.TypeParameter)
			{
				var implementationTParam = (ITypeParameterSymbol)implementation.ReturnType;
				var implementedTParam = (ITypeParameterSymbol)implemented.ReturnType;
				if (implementationTParam.Ordinal != implementedTParam.Ordinal)
					return false;
			}
			else
			{
				if (!implementation.ReturnType.Equals(implemented.ReturnType))
					return false;
			}

			if (implementation.Parameters.Length != implemented.Parameters.Length)
				return false;

			for(int i = 0; i < implementation.Parameters.Length; ++i)
			{
				IParameterSymbol implementationParam = implementation.Parameters[i];
				IParameterSymbol implementedParam = implemented.Parameters[i];

				if (!checkMatchingParameterTypes(implementationParam.Type, implementedParam.Type))
					return false;

				if (implementationParam.RefKind != implementedParam.RefKind)
					return false;
			}

			return true;
		}
		private static bool checkMatchingParameterTypes(ITypeSymbol implementationParamType, ITypeSymbol implementedParamType)
		{
			if (implementationParamType.TypeKind != implementedParamType.TypeKind)
				return false;
			if (implementationParamType.TypeKind == TypeKind.TypeParameter)
			{
				var implementationTParam = (ITypeParameterSymbol)implementationParamType;
				var implementedTParam = (ITypeParameterSymbol)implementedParamType;

				if (implementationTParam.DeclaringMethod == null && implementedTParam.DeclaringMethod == null)
				{
					if (!implementationTParam.Equals(implementedTParam))
						return false;
				}
				else if (implementationTParam.DeclaringMethod != null && implementedTParam.DeclaringMethod != null)
				{
					// do nothing so we fall through and return true; do not want to obscure the cascade of "return false" in this method
					// with an arbitrary "return true" in the middle here, which is why I'm leaving this empty.
				}
				else
					return false;
			}
			else
			{
				var namedImplementationParamType = implementationParamType as INamedTypeSymbol;
				var namedImplementedParamType = implementedParamType as INamedTypeSymbol;
				if (namedImplementationParamType == null || namedImplementedParamType == null)
					return false;
				if (namedImplementationParamType.Arity != namedImplementedParamType.Arity)
					return false;
				
				for(int i = 0; i < namedImplementationParamType.Arity; ++i)
				{
					if (!checkMatchingParameterTypes(namedImplementationParamType.TypeArguments[0], namedImplementedParamType.TypeArguments[0]))
						return false;
				}

				if (!implementationParamType.OriginalDefinition.Equals(implementedParamType.OriginalDefinition))
					return false;
			}
			return true;
		}
		private static bool checkMatchingTypeParameters(ITypeParameterSymbol implementationTParam, ITypeParameterSymbol implementedTParam)
		{
			if (implementationTParam.Variance != implementedTParam.Variance)
				return false;
			if (implementationTParam.HasConstructorConstraint != implementedTParam.HasConstructorConstraint)
				return false;
			if (implementationTParam.HasReferenceTypeConstraint != implementedTParam.HasReferenceTypeConstraint)
				return false;
			if (implementationTParam.HasValueTypeConstraint != implementedTParam.HasValueTypeConstraint)
				return false;
			if (implementationTParam.ConstraintTypes.Length != implementedTParam.ConstraintTypes.Length)
				return false;

			for (int i = 0; i < implementedTParam.ConstraintTypes.Length; ++i)
			{
				if (!implementationTParam.ConstraintTypes[i].Equals(implementedTParam.ConstraintTypes[i]))
					return false;
			}
			return true;
		}

		private static bool isStaticInterface(INamedTypeSymbol symbol)
		{
			if (symbol == null)
				return false;
			if (symbol.TypeKind != TypeKind.Interface)
				return false;

			return symbol.GetAttributes().Any(
				x => x.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Introspect.StaticAttribute"
			);
		}
		private static bool isStaticInterfaceImpl(INamedTypeSymbol symbol)
		{
			if (symbol.TypeKind != TypeKind.Interface)
				return false;

			if (symbol.TypeArguments.Length != 1)
				return false;

			if (symbol.TypeArguments[0].Kind == SymbolKind.ErrorType)
				return false;

			return symbol.ConstructedFrom.ToDisplayString(
				SymbolDisplayFormat.FullyQualifiedFormat
			) == "global::Introspect.IStatic<T>";
		}
	}
}
