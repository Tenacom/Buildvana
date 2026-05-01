# C# style guide

Coding style is mostly configured in `.editorconfig` and `.globalconfig` files.
Code style is enforced both in the editor and at build time: all style-related warnings become errors.

Here are additional rules that could not be codified in `*config` files.

## One type per file

Every type must be in its own file. No exceptions, even for delegates.

## File names

- Non-generic types: "TypeName.cs"
- Generic types: "TypeName`{N}.cs", where {N} is the number of type parameters. Example: `Foo<T, U>` goes in "Foo`2.cs"
- Embedded types: "OuterTypeName.InnerTypeName.cs"

## Modern C# features

Use latest C# features whenever possible. Non-exhaustive list of features to use:

- Collection expressions
- `field` keyword
- Primary constructors
- Extension blocks

Extension blocks need some warning suppression because of bugs in Roslyn analyzers. Use this template:

```csharp
/// <summary>
/// Provides extension methods for `MyClass` instances.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
partial class MyClassExtensions
{
    extension(MyClass @this)
    {
        // ...
    }
}
```

## Extension methods

The "this" parameter of extension methods MUST be named `@this`.

## Conditionals and loops

- NO multi-line conditions: use local variables or (possibly static) helpers for condition expressions that don't fit in a single line of reasonable length. Reserve helpers for reusable logic, or cases where several local variables are needed to make the condition readable. Use local variables for all other cases.
  Example:
  ```csharp
  // This is wrong
  if (foo is not null
      && bar is not null
      && baz is not null)
  {
      // ...
  }

  // This is correct
  var isValid = foo is not null
      && bar is not null
      && baz is not null;
  if (isValid)
  {
      // ...
  }
  ```

- ALWAYS use block statements, with opening and closing braces on separate lines, even if they contain just one instruction.
  Example:
  ```csharp
  // This is wrong
  if (a == 0) return;

  // This is correct
  if (a == 0)
  {
      return;
  }
  ```

## Ternaries

- Multi-line ternaries are fine for assignments and computations, not for `if` / `while` conditions.
- Do NOT use a ternary just for side effects: use `if` instead.

Use these templates:

```csharp
// Normal ternary (fits in one line)
result = foo is not null ? ComputeSomething(foo) : ComputeSomethingElse();
```

```csharp
// Multi-line ternary
result = foo is not null
    ? ComputeSomething(firstParam, secondParam, foo)
    : ComputeSomethingElse();
```

```csharp
// Concatenated ternaries
result = foo is not null ? ComputeSomething(firstParam, secondParam, foo)
    : bar is not null ? ComputeSomething(firstParam, secondParam, bar)
    : someOtherCondition ? ComputeSomething(firstParam, secondParam, 2)
    : ComputeSomethingElse();
```
