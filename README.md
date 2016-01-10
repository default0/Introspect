# Introspect
Introspect is a C# library that adds classes and methods for Design by Introspection

Introspect adds 4 main classes for implementing design by Introspection:
- Introspecter
- StaticInterface
- StaticDuckInterface
- DuckInterface

## How to Use

Simply install via NuGet
```
Install-Package Introspect
```

[NuGet Project Page](https://www.nuget.org/packages/Introspect/1.0.5853.28048)

## Introspecter

The Introspecter class is used to determine capabilities of generic types or objects.
It exposes the following methods:
- IsDuck
- IsImplementation

### IsDuck

```csharp
public static bool IsDuck<TImpl, TInterface>()
```

Tests whether the type TImpl implements all methods specified by TInterface.
If TInterface is a *static interface*, this method will check for implementation of static
methods.

#### Example

```csharp
public interface IDuck
{
	void Quack();
}
public class SecretlyADuck
{
	public void Quack() => Console.WriteLine("Quack!");
}
public class NotADuck
{
	public void Meow() => Console.WriteLine("Meow!");
}

public static void Main()
{
	Console.WriteLine(Introspecter.IsDuck<SecretlyADuck, IDuck>()); // true
	Console.WriteLine(Introspecter.IsDuck<NotADuck, IDuck>()); // false
}
```

Sometimes, you want to test whether a given object is a duck, instead of a given type.
```csharp
public static bool IsDuck<TInterface, TImpl>(TImpl impl)
```

This overload of IsDuck tests whether the given object implements all methods defined by TInterface.
TInterface may not be a *static interface*.

#### Example

```csharp
public interface IDuck
{
	void Quack();
}
public abstract class Animal
{
	private int age;
	void Age() => ++age;
}
public class Duck : Animal
{
	public void Quack() => Console.WriteLine("Quack!");
}
public class Cat : Animal
{
	public void Meow() => Console.WriteLine("Meow!");
}
public class Fish : Animal
{
	public void Swim() => Console.WriteLine("A fish is swimming!");
}
public void ProcessAnimals(Animal[] animals)
{
	foreach(Animal animal in animals)
	{
		if(Introspecter.IsDuck<IDuck>(animal))
			DuckInterface<IDuck>.Duck(animal).Quack();
		
		animal.Age();
	}
}
```

This is useful if some of the objects in an enumeration need special treatment that should not be exposed via the base class.
In this case, a MakeNoise() function in the Animal class would be a bad abstraction, since Fish do not make noises and thus
should not need to implement this function.

### IsImplementation

```csharp
public static bool IsImplementation<TImpl, TInterface>()
```

Tests whether the TImplementation type explicitly implements the TInterface type.
This method is mostly useful for checking whether a given type implements a static interface.
Just using `if(variable is IInterface)` is a better solution if you want to check whether a regular
interface is implemented.

#### Example

```csharp
[Static]
public interface IFoo
{
	int Bar();
}
public class Foo : IStatic<IFoo>
{
	public int Bar() => 42;
}
public class FooDuck
{
	public int Bar() => 1337;
}
public static void Main()
{
	Console.WriteLine(Introspecter.IsImplementation<Foo, IFoo>()); // true
	Console.WriteLine(Introspecter.IsImplementation<FooDuck, IFoo>()); // false, does not explicitly implement IStatic<IFoo>
	Console.WriteLine(Introspecter.IsDuck<FooDuck, IFoo>()); // true, contains all methods required by IFoo
}
```

## StaticInterface

The `StaticInterface` class is useful if you want to require that certain methods
are present on a type (ie declared as `public static`) instead of on an instance
of that type.

To declare a static interface, use the `[Static]` attribute.

#### Example

```csharp
public interface INonStaticInterface { }

[Static]
public interface IStaticInterface { }
```

If an interface is not marked with the `[Static]` attribute it cannot be used
by the `StaticInterface` class.

In order to implement a *static interface*, you have to implement the `IStatic<T>`
interface.

#### Example

```csharp
[Static]
public interface IStaticInterface
{
	void Bar();
}

public class WrongImplementation : IStaticInterface
{
}

public class CorrectImplementation : IStatic<IStaticInterface>
{
	public static void Bar() => Console.WriteLine("Bar!");
}
```

The reason for that is that directly inheriting `IStaticInterface` would make the compiler
complain if you did not provide non-static implementations for methods of the interface.

Static interfaces _cannot contain indexers_. This is because there is no way to declare
a static indexer in C#.

This means that the following is an error:
```csharp
[Static]
public interface IError
{
	int this[int index] { get; }
}
```

Aside from this exception, static interfaces work just like normal interfaces, only the
implementing methods have to be `public static`.

In order to call methods of a static interface, you have to use the `StaticInterface` class.

#### Example

```csharp
[Static]
public interface IFoo
{
	void Bar();
}

public class FooImpl : IStatic<IFoo>
{
	public static void Bar() => Console.WriteLine("Bar!");
}
public class OtherFooImpl : IStatic<IFoo>
{
	public static void Bar() => console.WriteLine("Other Bar!");
}

public static void Use<T>() where T : IStatic<IFoo>
{
	StaticInterface<T, IFoo>.Impl.Bar(); // Use<FooImpl> will print "Bar!", Use<OtherFooImpl> will print "Other Bar!"
}
```

Note the `Impl` property has to be used to invoke methods implemented by the interface.

## StaticDuckInterface

Since C# does not natively provide a way to declare and use static interfaces, if you use 3rd party
libraries whose source you do not control, you sometimes encounter several types that
share a conceptual static interface, but this interface is not declared explicitly for lack of
being able to express this concept in the language.

For this purpose, `StaticDuckInterface` exists. `StaticDuckInterface` does not require that
an implementation explicitly implement `IStatic<T>`, but only requires that the
implementation has `public static` methods matching all the methods defined in the
interface.

A popular example of this is the `Parse(string)` method defined on most primitive types
such as `int`, `long`, etc.

Using `StaticDuckInterface` you can have a generic method that parses these types.

#### Example

```csharp
[Static]
public interface IParseable<T>
{
	T Parse(string str);
}

public static T Parse<T>(string str)
{
	if(!Introspecter.IsDuck<T, IParseable<T>>)
		throw new Exception($"Type {typeof(T).FullName} cannot be parsed because it contains no public static Parse(string) method returning a {typeof(T).FullName}");
	
	return StaticDuckInterface<IParseable<T>, T>.Impl.Parse(str);
}
```

## DuckInterface

`DuckInterface` does the same as `StaticDuckInterface` conceptually, but it works
on objects instead of types and cannot use static interfaces.

#### Example

```csharp
public interface ITypeCoded
{
	TypeCode GetTypeCode();
}
public static TypeCode GetTypeCode<T>(T obj)
{
	if(!Introspecter.IsDuck<ITypeCoded>(obj))
		throw new Exception($"Cannot get type code of Type {typeof(T).FullName} because it contains no public GetTypeCode() method returning a TypeCode.");
	
	return DuckInterface<ITypeCoded>.Duck(obj).GetTypeCode();
}
```

This is much less useful than the `StaticDuckInterface` since often there are very viable alternatives
such as simply `is` checking and casting, however it can be useful in certain circumstances where
a type lacks explicit specification of implemented interfaces or if you want to check only for a subset
of an implemented interface (such as in the above example, where instead of requiring a full blown
`IConvertible` interface we just want to check if the object has the `GetTypeCode()` method).