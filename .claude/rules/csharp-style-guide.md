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
- Pattern matching (see below)

## Pattern matching

Prefer pattern matching over equivalent chains of comparisons, type checks, and casts: a pattern states the shape of the data, while a chain of checks makes the reader reassemble it. In particular:

- List patterns over count checks + indexed comparisons:

  ```csharp
  // This is wrong
  var isPinPath = propertyPath.Count == 2 && propertyPath[0] == PropertyName && propertyPath[1] == PackageId;

  // This is correct
  var isPinPath = propertyPath is [PropertyName, PackageId];
  ```

- `is T x` over `as` + null check, and over `is` check + cast.
- Property patterns over chains of dotted comparisons: `result is { ExitCode: 0, StandardError.Length: 0 }`.
- `is null` / `is not null` over `== null` / `!= null`.
- Switch expressions over `if`/`else if` chains or switch statements whose branches produce a value.

Semantic differences to keep in mind when converting:

- List, property, and recursive patterns imply a null test: `propertyPath is [PropertyName, PackageId]` is `false` when `propertyPath` is null, where `propertyPath.Count == 2` would throw. Usually an improvement, but it is a behavior change, not a pure rewrite.
- Constant patterns never use the type's `==` operator. Non-null constants compare with `Equals`; `null` is a plain reference test (a `HasValue` check for nullable value types). With `float` / `double` the pattern is what you want: `x is double.NaN` matches NaN, while `x == double.NaN` is always `false`. Exception: in the rare case of a type that overloads `==` so that `x == null` and `x is null` disagree, keep the operator form.

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
public static class MyClassExtensions
{
    extension(MyClass @this)
    {
        // ...
    }
}
```

The "this" parameter MUST be named `@this`.

Add `partial` only when the class is actually split across files, as described in "Partial classes" above and "Extension classes" below. ReSharper's `PartialTypeWithSinglePart` flags a partial class with a single part, and every warning is an error here.

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

## Line length

Code and comment lines are limited to 140 characters, including indentation. The limit covers C# source in full: code, ordinary comments, and XML documentation comments alike. Prose files (Markdown and the like) have no line-length limit.

The limit is a review rule, not a formatter setting: there is deliberately no `max_line_length` in `.editorconfig`, so no tool reformats existing code behind your back.

New and modified lines always comply. Beyond that, leave the file cleaner than you found it: when you are actually working in a file — adding to it, reworking it, fixing something in it — bring the whole file within the limit, and commit the leftover wraps separately, so that the change under review stays readable. A mechanical sweep that touches many files with a line or two each does not count as working in them: comply on the lines you touch, and leave the rest.

Declarations are held to a stricter limit; see "Long parameter lists" below.

## Long parameter lists

When a parameter list makes a declaration exceed 120 characters including indentation, wrap it. Declarations are held to a stricter limit than the 140 characters allowed elsewhere, because a signature is the one line a reader has to parse in full in order to use the member. The rule covers methods, constructors (including primary constructors), records, delegates, indexers, and local functions.

Invocations are not subject to the 120-character threshold: they follow the general 140-character limit. When a call does have to be wrapped — because it exceeds 140 characters, or because its argument list is long enough that one argument per line simply reads better — wrap it with the mechanics below, which apply to parameter and argument lists alike, base/this constructor calls (`: base(...)` / `: this(...)`) included.

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

## ReSharper suppressions

Canonical form: `// ReSharper disable once <DiagnosticId> // <justification>` — the justification comment is mandatory.
