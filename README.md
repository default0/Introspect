# Introspect
Introspect is a C# library that adds classes and methods for Design by Introspection

Introspect adds 4 main classes for implementing design by Introspection:
- Introspecter
- StaticInterface
- StaticDuckInterface
- DuckInterface

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