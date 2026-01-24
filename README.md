# AccessItEasy

A compile-time alternative to `UnsafeAccessor` for accessing private members. Uses [Fody](https://github.com/Fody/Fody) to weave IL instructions at build time.

## Features

- **Zero runtime overhead** - Inlined by JIT in most cases
- **Compile-time validation** - No runtime exceptions for missing members
- **Wide .NET support** - Works with .NET Framework 4.6+ and all .NET versions
- **Flexible generics** - Supports open generic type accessors
- **AOT compatible** - Works in dynamic assembly loading scenarios
- **Clean output** - All attributes removed after weaving

## Comparison with UnsafeAccessor

| Aspect                          | AccessItEasy                           | UnsafeAccessor                   |
| ------------------------------- | -------------------------------------- | -------------------------------- |
| **Binding Time**                | Compile-time (Fody weaving)            | Runtime                          |
| **Error Detection**             | Build errors                           | Runtime exceptions               |
| **.NET Version**                | .NET Framework 4.6+, all .NET versions | .NET 8+ (varying support levels) |
| **Generics**                    | Flexible open generic accessors        | Limited support                  |
| **Code Size**                   | Larger (IL embedded in assembly)       | Smaller (generated at runtime)   |
| **AOT + Dynamic Loading**       | Works                                  | May crash CLR                    |
| **Behavior on Assembly Update** | May differ slightly (unconfirmed)      | Runtime resolution               |

### When to Choose AccessItEasy

- Targeting .NET Framework or .NET versions before 8
- Need compile-time validation of accessor declarations
- Using AOT with dynamically loaded assemblies
- Want flexible generic accessor patterns

### When to Choose UnsafeAccessor

- Targeting .NET 8+ exclusively
- Want minimal code size
- Don't need compile-time validation
- Don't want to use undocumented feature

## Installation

Add the following to your project file:

```xml
  <ItemGroup>
    <PackageReference Include="Fody" Version="6.9.3">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <ProjectReference Include="AccessItEasy\AccessItEasy.csproj">
      <!-- You don't need AccessItEasy at runtime -->
      <Private>false</Private>
    </ProjectReference>
    <ProjectReference Include="AccessItEasy.Fody\AccessItEasy.Fody.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
      <OutputItemType>WeaverFiles</OutputItemType>
    </ProjectReference>
  </ItemGroup>
```

Add `FodyWeavers.xml` to your project root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="FodyWeavers.xsd">
  <AccessItEasy />
</Weavers>
```

## Usage

### Accessing Private Fields

```csharp
public class Target
{
    private int _secretValue = 42;
}

// Define accessor
[PrivateAccessor(PrivateAccessorKind.Field, Name = "_secretValue")]
private static extern int GetSecretValue(Target target);

[PrivateAccessor(PrivateAccessorKind.Field, Name = "_secretValue")]
private static extern void SetSecretValue(Target target, int value);

// Usage
var target = new Target();
int value = GetSecretValue(target);      // Returns 42
SetSecretValue(target, 100);             // Sets _secretValue to 100
```

### Accessing Private Static Fields

```csharp
public class Target
{
    private static int _staticCounter = 0;
}

[PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticCounter")]
private static extern int GetStaticCounter(Target _);  // First param is type marker

[PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticCounter")]
private static extern void SetStaticCounter(Target _, int value);
```

### Calling Private Methods

```csharp
public class Target
{
    private int Add(int a, int b) => a + b;
    private static string Format(string template, int value) => string.Format(template, value);
}

// Instance method
[PrivateAccessor(PrivateAccessorKind.Method, Name = "Add")]
private static extern int CallAdd(Target target, int a, int b);

// Static method
[PrivateAccessor(PrivateAccessorKind.StaticMethod, Name = "Format")]
private static extern string CallFormat(Target _, string template, int value);
```

### Invoking Private Constructors

```csharp
public class Target
{
    private Target(int value) { /* ... */ }
}

[PrivateAccessor(PrivateAccessorKind.Constructor)]
private static extern Target CreateTarget(int value);
```

### Generic Support

```csharp
public class Box<T>
{
    private T _value;
}

// Generic accessor method
[PrivateAccessor(PrivateAccessorKind.Field, Name = "_value")]
private static extern T GetValue<T>(Box<T> box);

[PrivateAccessor(PrivateAccessorKind.Constructor)]
private static extern Box<T> CreateBox(T value);

// Generic accessor in generic class
private class BoxHelper<T>
{
    [PrivateAccessor(PrivateAccessorKind.Field, Name = "_value")]
    public static extern T GetValue(Box<T> box);

    [PrivateAccessor(PrivateAccessorKind.Constructor)]
    public static extern Box<T> Create(T value);
}

// Usage
var intBox = BoxHelper<int>.Create(42); // or CreateBox<int>(42) directly
int value = BoxHelper<int>.GetValue(intBox);
```

### Accessing Inaccessible Types with `PrivateAccessorType`

When the target type itself is not accessible (e.g., internal types from external assemblies), use `PrivateAccessorType` to specify types by name:

```csharp
// When we can't reference Vector2 directly
[PrivateAccessor(PrivateAccessorKind.Method, Name = "Add")]
private static extern void CallAdd(
    [PrivateAccessorType("System.Collections.Generic.List`1[System.Numerics.Vector2]")] object list,
    [PrivateAccessorType("System.Numerics.Vector2")] object item);

// For return types
[PrivateAccessor(PrivateAccessorKind.Field, Name = "Value")]
[return: PrivateAccessorType("System.Numerics.Vector2")]
private static extern object GetValue(HasVector2Value target);
```

## Benchmarks

Private field access performance comparison:

| Method                      | Mean          | Code Size | Allocated |
| --------------------------- | ------------- | --------- | --------- |
| DirectPublicAccess          | 0.00 ns       | 8 B       | -         |
| **PrivateAccessor_Get**     | **0.00 ns**   | **8 B**   | **-**     |
| **PrivateAccessor_Ref_Get** | **0.00 ns**   | **8 B**   | **-**     |
| UnsafeAccessor_Get          | 0.00 ns       | 8 B       | -         |
| ReflectionCached_Get        | 23.86 ns      | 86 B      | 24 B      |
| ExpressionTree_Get          | 0.34 ns       | 16 B      | -         |
| ILEmit_Get                  | 1.20 ns       | 16 B      | -         |
| Delegate_Get                | 0.35 ns       | 16 B      | -         |
| DirectPublicAccess_Set      | 0.0003 ns     | 12 B      | -         |
| **PrivateAccessor_Set**     | **0.0015 ns** | **12 B**  | **-**     |
| **PrivateAccessor_Ref_Set** | **0.0011 ns** | **12 B**  | **-**     |
| UnsafeAccessor_Set          | 0.0105 ns     | 12 B      | -         |
| ReflectionCached_Set        | 26.0690 ns    | 164 B     | 24 B      |
| ExpressionTree_Set          | 0.3372 ns     | 22 B      | -         |
| ILEmit_Set                  | 1.2175 ns     | 22 B      | -         |
| ILEmit_Ref_Set              | 1.8459 ns     | 30 B      | -         |
| Delegate_Set                | 0.3314 ns     | 22 B      | -         |

- AccessItEasy achieves almost **zero overhead** - identical performance to direct field access
- Performance matches `UnsafeAccessor` exactly
- No heap allocations

We have a benchmark project for all accessing scenarios, run it yourself to see the results: [AccessItEasy.Benchmarks](AccessItEasy.Benchmarks)

## How It Works

AccessItEasy uses [Fody](https://github.com/Fody/Fody) to weave IL instructions at compile time. When you declare:

```csharp
[PrivateAccessor(PrivateAccessorKind.Field, Name = "_value")]
private static extern int GetValue(Target target);
```

Fody transforms it into:

```csharp
private static int GetValue(Target target)
{
    return target._value;  // Direct field access IL
}
```

The weaver also:

- Removes all `PrivateAccessor` and `PrivateAccessorType` attributes
- Adds `[CompilerGenerated]` attribute to the method
- Generates `IgnoresAccessChecksToAttribute` for cross-assembly access

### About IgnoresAccessChecksTo

To break access restrictions, AccessItEasy relies on the runtime "magic" of [IgnoresAccessChecksTo](https://github.com/KirillOsenkov/Bliki/wiki/IgnoresAccessChecksTo) to bypass access checks. While this is an undocumented feature, it is widely used by many well-known libraries:

- NHibernate
- System.Reflection.DispatchProxy
- VS-MEF
- dnSpy
- Microsoft Orleans
- Castle.Core

## Requirements

- [Fody](https://github.com/Fody/Fody) 6.0+
- .NET Framework 4.6+ or .NET Standard 2.0+

## License

MIT License
