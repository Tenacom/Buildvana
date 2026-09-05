# C# style guide

`.editorconfig` and `.globalconfig` configure most of the coding style. The editor and the build both enforce it, and every style warning is an error.

The rules below are the ones those files cannot express.

## One type per file

Every type goes in its own file, delegates included.

## File names

- Non-generic types: `TypeName.cs`.
- Generic types: ``TypeName`{N}.cs``, where `{N}` is the generic type arity. Example: `Foo<T, U>` goes in `Foo`2.cs`.
- Embedded types: `OuterTypeName.InnerTypeName.cs`.
- Generic embedded types: ``OuterTypeName.InnerTypeName`{N}.cs``, ``OuterTypeName`{M}.InnerTypeName`{N}.cs``.

## Modern C# features

Use the latest C# features. Among them:

- Collection expressions
- `field` keyword
- Primary constructors
- Extension blocks
- Pattern matching (see below)

## Pattern matching

Prefer pattern matching over an equivalent chain of comparisons, type checks, and casts. A pattern states the shape of the data. A chain of checks makes the reader reassemble it. In particular:

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

- List, property, and recursive patterns imply a null test. `propertyPath is [PropertyName, PackageId]` is `false` when `propertyPath` is null, where `propertyPath.Count == 2` would throw. That is usually an improvement, but it is a behavior change, not a pure rewrite.
- Constant patterns never use the type's `==` operator. A non-null constant compares with `Equals`. `null` is a plain reference test, or a `HasValue` check for a nullable value type. With `float` and `double` the pattern is what you want: `x is double.NaN` matches NaN, while `x == double.NaN` is always `false`. Exception: when a type overloads `==` so that `x == null` and `x is null` disagree, keep the operator form.

## Partial classes

When a class exceeds 500 lines of code, or has nested types, split it into partial class files.

The main file, the one with the class declaration, is named `TypeName.cs`. The other files are named:

- `TypeName.NestedTypeName.cs` for nested types.
- `TypeName-MethodName.cs` for a file that contains all the overloads of a method.
- `TypeName-maxThreeWordDescription.cs` for a file that contains code related to a specific aspect.
- `SomeFunctionalityExtensions-ExtendedType.cs` for the extension block for `ExtendedType`, in an extension class that extends several types. See "Extension classes" below.

Use the generic type rule "TypeName`{N}" for both outer and nested generic types.

Class modifiers, such as access, `static`, and `abstract`, go in the main file only.

The XML comment of the class goes in the main file only.

When a class splits cleanly into two or more files, the main class block may be empty. A class with three methods of a dozen overloads each is an example.

## Member ordering

StyleCop's SA1204 and its siblings already bucket members by access and staticness. Within a single bucket, order by the call graph. A method that calls another goes above the methods it calls.

The reader then meets the high-level method first and reads down into its callees. No analyzer enforces this order. Apply it when adding methods and when reorganizing existing ones.

- The rule applies only within a bucket. SA1204 still wins across buckets: private static members stay before private instance ones, and so on.
- When adding a helper, put it below its caller, never above.
- Peer methods are methods called by the same parent, neither calling the other. Order them as the parent calls them. That order is secondary to caller-before-callee.

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

The receiver parameter must be named `@this`.

Add `partial` only when the class is split across files, as described in "Partial classes" above and "Extension classes" below. ReSharper's `PartialTypeWithSinglePart` flags a partial class with a single part, and every warning is an error here.

Do not mix extension blocks with regular static methods in the same class. "Partial classes" above and "Extension classes" below say how to split an extension class into files.

Do not infer the convention from existing files. This section is the convention.

## Extension classes

Prefer one extension block per class. Use multiple blocks only when they provide related functionality and share private helpers, and split them into separate files as the rules below describe.

### Classes that extend a single type

An extension class that extends a single type is named after the extended type:

- Strip the initial "I" when the extended type is an interface.
- Use the .NET class name, not the C# keyword: "String", not "string".
- Add an "Extensions" suffix.

**Examples:**

- `MyClass` extensions go in `MyClassExtensions`.
- `MyGenericClass<T>` extensions go in ``MyGenericClassExtensions``, because extension classes cannot be generic.
- `IMyInterface` extensions go in `MyInterfaceExtensions`.

When such a class has private helper methods, do not mix them with the extension block. Make the class partial and put the private methods in a separate file, such as `MyClassExtensions-private.cs`. Do this even when the class is under the size threshold for partial classes.

### Classes that extend more than one type

An extension class may hold more than one extension block when the blocks provide related functionality to several types and share private helpers. Then the class name reflects the functionality. The main class file holds the private helpers, and each extension block goes in a separate file.

**Example:** `EmojiExtensions` provides a `StripEmojis` method for strings, read-only spans, and related types. It is split into these files:

- `EmojiExtensions.cs`: XML comment, class modifiers, private helpers.
- `EmojiExtensions-String.cs`: extension block for `string` (use the .NET class name in the file name, not the keyword).
- ``EmojiExtensions-ReadOnlySpan`1.cs``: extension block for `ReadOnlySpan<char>` (same arity convention as for inner types).
- `EmojiExtensions-StringBuilder.cs`: extension block for `StringBuilder`.
- `EmojiExtensions-Formattable.cs`: extension block for `IFormattable`.

## Conditionals and loops

- No multi-line conditions. When a condition does not fit in one line, put it in a local variable, or in a helper method. Reserve a helper for reusable logic, or for a condition that needs several local variables to read well. Use a local variable in every other case.
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

- Always use block statements, with the braces on their own lines, even for one instruction.
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
- Do not use a ternary for side effects. Use `if` instead.

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

## Culture and formatting

This project is single-culture. Its diagnostics are English-only and must read identically on a developer machine and on a CI runner.

- Never give an API an `IFormatProvider` or `CultureInfo` parameter. Use `CultureInfo.InvariantCulture` inside the implementation instead.
- In particular, do not mirror the shape of `string.Format` with a nullable provider parameter. Passing `null` there resolves to `CurrentCulture`, so the parameter's only effect is locale-dependent output by accident.
- Do not propose a provider-taking overload "for flexibility". That flexibility goes unused, and the parameter only invites mistakes.

## String literals

Pick the form that shows the content most clearly. No form wins in every case, so ReSharper's `UseRawString`, `UseVerbatimString`, and `RawStringCanBeSimplified` are all suppressed. They disagree with each other by design, and the choice is one an inspection cannot make.

- **Raw** (`"""..."""`) when the content contains quotes. This is why the regexes in `SelfReferenceUpdater` are readable: `[^""]+` becomes `[^"]+`, shorter as well as clearer. Quotes decide, not backslashes. Raw and verbatim both take a `\` literally, so a backslash alone is no reason to prefer one over the other.
- **Raw, multi-line** for content that is several lines by nature: JSON documents, expected output, code templates. The indentation of the closing `"""` sets the margin stripped from every line. The literal then lines up with the code around it, and needs no `\n` and no concatenation. Prefer this form to a single-line literal joined with `\n` whenever the content is multi-line.
- **Regular, with escapes**, when the escape is the point. In a test that asserts line and column, `"{\n  \"name\": 42\n}"` keeps its newlines explicit and independent of the file's line endings. A multi-line raw literal would make them a property of the source file.
- **Verbatim** (`@"..."`) for content heavy in backslashes and free of quotes, such as Windows paths. Raw strings cover every other case it used to serve.
- Prefer **consistency with adjacent literals** over the shortest form for each. Where several literals form a group, matching forms read better than one odd member, even when that member would compile as a plain `"..."`.

## Line length

A line of C# is at most 140 characters, including indentation. The limit covers code, ordinary comments, and XML documentation comments. Prose files, such as Markdown, have no line-length limit.

The limit is a review rule, not a formatter setting. `.editorconfig` has no `max_line_length` on purpose, so no tool reformats existing code on its own.

New and modified lines always comply. When you work in a file, by adding to it, reworking it, or fixing something in it, bring the whole file within the limit. Commit those extra wraps separately, so that the change under review stays readable. A mechanical sweep that touches a line or two in many files does not count as working in them. There, comply on the lines you touch and leave the rest.

Declarations have a stricter limit. See "Long parameter lists" below. Whole-file cleanup covers that limit too. A file within the general limit but with over-long declarations still owes a second sweep.

## Long parameter lists

When a parameter list makes a declaration exceed 120 characters including indentation, wrap it. A declaration has a stricter limit than the 140 characters allowed elsewhere, because a reader parses a signature in full to use the member. The rule covers methods, constructors, primary constructors included, records, delegates, indexers, and local functions.

An invocation follows the general 140-character limit, not the 120-character one. Wrap a call when it exceeds 140 characters, or when its argument list is long enough that one argument per line reads better. The mechanics below apply to parameter lists, argument lists, and base or this constructor calls (`: base(...)`, `: this(...)`) alike.

When wrapping:

- Put every parameter or argument on its own line. Do not leave one parameter next to the opening parenthesis with the rest on their own lines.
- Indent wrapped parameters or arguments one level beyond the line that contains the opening parenthesis.
- The closing parenthesis goes on the same line as the last parameter or argument, per StyleCop rule SA1111.
- Generic constraints (`where T : ...`) go on their own lines after the closing parenthesis. With several constraint clauses, each `where` goes on its own line, indented at the same level as the parameters. StyleCop SA1127 forbids two `where` clauses on one line.
- Constructor chaining (`: base(...)` or `: this(...)`) goes on its own line before the opening brace, indented at the same level as the parameters. When the chained call is itself long enough to wrap, apply the same rules to its argument list.

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

Canonical form: `// ReSharper disable once <DiagnosticId> // <justification>`. The justification comment is mandatory.
