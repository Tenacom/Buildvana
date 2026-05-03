# C# style guide

Coding style is mostly configured in `.editorconfig` and `.globalconfig` files.
Code style is enforced both in the editor and at build time: all style-related warnings become errors.

Here are additional rules that could not be codified in `*config` files.

## One type per file

Every type must be in its own file. No exceptions, even for delegates.

## File names

- Non-generic types: `TypeName.cs`.
- Generic types: ``TypeName`{N}.cs``, where `{N}` is the generic type arity. Example: `Foo<T, U>` goes in `Foo`2.cs`.
- Embedded types: `OuterTypeName.InnerTypeName.cs`.
- Generic embedded types: ``OuterTypeName.InnerTypeName`{N}.cs``, ``OuterTypeName`{M}.InnerTypeName`{N}.cs``.

## Modern C# features

Use latest C# features whenever possible. Non-exhaustive list of features to use:

- Collection expressions
- `field` keyword
- Primary constructors
- Extension blocks

## Partial classes

Use partial classes to split a class into multiple files if it exceeds 500 lines of code, or for nested types.

The "main" file containing the class declaration should be named "TypeName.cs". Other files should be named:

- `TypeName.NestedTypeName.cs` for nested types.
- `TypeName-MethodName.cs` for a file that contains all the overloads of a method.
- `TypeName-maxThreeWordDescription.cs` for a file that contains code related to a specific aspect.
- `SomeFunctionalityExtensions-ExtendedType.cs` for a file that contains an extension block for `ExtendedType`, in the context of an extension class that extends multiple types with related functionality (see the "Extension classes" section below).

Use the generic type rule "TypeName`{N}" for both outer and nested generic types.

Class modifiers (access, static, abstract, etc.) should be specified in the main file, and omitted from other files.

XML comments for the class should be in the main file, and omitted from other files.

If a class can be clearly split into two or more separate files (for example, if it has three methods, each with a dozen overloads), the main class block may be empty.

## Extension blocks

Extension blocks need some warning suppression because of bugs in Roslyn analyzers. Always use this template, adjusting names and access modifier as needed:

```csharp
/// <summary>
/// Provides extension methods for `MyClass` instances.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static partial class MyClassExtensions
{
    extension(MyClass @this)
    {
        // ...
    }
}
```

The "this" parameter MUST be named `@this`.

Do not mix extension blocks with regular static methods of the same class. Refer to "Partial classes" above and "Extension classes" below for splitting extension classes into multiple files if needed.

Do not look into existing files to understand the convention in place: this _is_ the convention.

## Extension classes

Prefer one extension block per class. Use multiple blocks only when they provide related functionality and share private helpers, and split them into separate files as described in the rules below.

### Classes that extend a single type

Extension classes that extend a single type are named after the extended type, following these rules:

- strip the initial "I" if the extended type is an interface;
- use .NET class names, not C# keywords (e.g., "String" instead of "string");
- add an "Extensions" suffix.

**Examples:**

- `MyClass` extensions go in `MyClassExtensions`.
- `MyGenericClass<T>` extensions go in ``MyGenericClassExtensions``, because extension classes cannot be generic.
- `IMyInterface` extensions go in `MyInterfaceExtensions`.

If such an extension class contains private helper methods, they should not be mixed with the extension block. Instead, make a partial class and split private methods into a separate file, e.g., `MyClassExtensions-private.cs`.
Do this even if the whole class wouldn't exceed the threshold for partial classes.

### Classes that extend more than one type

An extension class may contain more than one extension block if they provide related functionality to multiple types using shared private helpers.
In this case, the class name should reflect the provided functionality and extension blocks should not be in the main class file.
The main class file contains private helpers; extension blocks always go in separate files.
**Example:** class `EmojiExtensions`, which provides a "StripEmojis" method for strings, read-only spans, and related types, is split into these files:

- `EmojiExtensions.cs` - XML comment, class modifiers, private helpers.
- `EmojiExtensions-String.cs` - extension block for `string` (use the .NET class name in the file name, not the keyword).
- ``EmojiExtensions-ReadOnlySpan`1.cs`` - extension block for `ReadOnlySpan<char>` (same arity convention as for inner types).
- `EmojiExtensions-StringBuilder.cs` - extension block for `StringBuilder`.
- `EmojiExtensions-Formattable.cs` - extension block for `IFormattable`.

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

## Long parameter lists

When a parameter list (in a declaration) or argument list (in an invocation) makes the line exceed 120 characters including indentation, wrap it. This rule applies to:

- declarations: methods, constructors (including primary constructors), records, delegates, indexers, local functions;
- invocations: any call site of the above, plus base/this constructor calls (`: base(...)` / `: this(...)`).

The 120-character threshold is specific to this rule and is independent of any editor or formatter line-length setting (the project intentionally has no `max_line_length` in `.editorconfig`).

When wrapping:

- Put every parameter or argument on its own line. Do not mix one parameter next to the opening parenthesis with the rest on their own lines — it's all or nothing.
- Indent wrapped parameters or arguments one level beyond the line that contains the opening parenthesis.
- The closing parenthesis goes on the same line as the last parameter or argument, per StyleCop rule SA1111.
- Generic constraints (`where T : ...`) go on their own lines after the closing parenthesis. With multiple constraint clauses, each subsequent `where` goes on its own line, indented at the same level as the parameters (StyleCop SA1127 forbids placing two `where` clauses on the same line).
- Constructor chaining (`: base(...)` or `: this(...)`) goes on its own line before the opening brace, indented at the same level as the parameters. If the chained call is itself long enough to wrap, apply the same rules to its argument list.

**Examples:**

Declaration, wrong:

```csharp
    public async Task<ProcessResult> RunAsync(string executable, IEnumerable<string> args, string? workingDirectory = null, bool throwOnNonZero = true, Action<string>? onStdout = null, CancellationToken cancellationToken = default)
    {
        // ...
    }
```

Declaration, correct:

```csharp
    public async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> args,
        string? workingDirectory = null,
        bool throwOnNonZero = true,
        Action<string>? onStdout = null,
        CancellationToken cancellationToken = default)
    {
        // ...
    }
```

Invocation, wrong:

```csharp
        var result = await runner.RunAsync(executable, args, workingDirectory, throwOnNonZero: true, onStdout: line => log.WriteLine(line), cancellationToken).ConfigureAwait(false);
```

Invocation, correct:

```csharp
        var result = await runner.RunAsync(
            executable,
            args,
            workingDirectory,
            throwOnNonZero: true,
            onStdout: line => log.WriteLine(line),
            cancellationToken).ConfigureAwait(false);
```
