# File formats

`.editorconfig` configures the file format rules. The rules below are the ones it cannot express, or the ones better known in advance.

## Common defaults for all files

A section below overrides them for its own file type.

- Charset: UTF-8 without BOM
- Line separator: LF
- Indentation: spaces, not tabs
- Tab width = indentation width = 4
- A trailing newline is required

## C# source files (`*.cs`)

- Charset: UTF-8 with BOM. StyleCop's SA1412 fails the build on a missing BOM.

### Creating new C# files

The `Write` tool strips the leading BOM, even when U+FEFF is embedded in the content, so it cannot create a `.cs` file. Use this workflow instead:

1. Copy `.claude/templates/Default.cs` to the target path. The template carries the BOM and the standard copyright preamble.
2. Use the `Edit` tool to replace `// __EVERYTHING_GOES_HERE__` with the file body. `Edit` preserves the BOM.

When you must use `Write` to rewrite an existing `.cs` file in full, which also strips the BOM, prepend `0xEF 0xBB 0xBF` to the file afterwards.

## MSBuild XML files (`*.*proj`, `*.props`, `*.targets`)

- No prolog (`<?xml ... ?>`)
- Tab width = indentation width = 2

## Other XML files

- Prolog (`<?xml ... ?>`) usually required

## Markdown files (`*.md`)

- Tab width = indentation width = 2
- Markdown line break: 2 spaces
- Always use `_` for emphasis and `**` for strong emphasis. This applies to every `.md` file, AI-consumed ones included. markdownlint rule MD049 is a backup enforcement, not the source of the rule.  
  Example: `_emphasis_` and `**strong emphasis**` are correct. `*emphasis*` and `__strong emphasis__` are not.

Honor the markdownlint rules in `.markdownlint-cli2.jsonc`. Suppress a rule with an XML comment only when there is no other way. Example:

```markdown
<!-- markdownlint-disable MD036 -->
**This line will not be flagged as using emphasis as heading**
<!-- markdownlint-enable MD036 -->
```

Markdown files consumed by AIs, such as `CLAUDE.md` and the files in `.claude`, are exempt from markdownlint rules.

## JSON files (`*.json`, `*.jsonc`, `*.json5`)

- Tab width = indentation width = 2
- Use comments in `.jsonc` files, JSON5 features in `.json5` files.
- Do not use comments or JSON5 features in a `.json` file, unless instructed to, or unless the file uses them already. Some tools accept comments or JSON5 features in a `.json` file. Do not assume that a tool does, but use the features a file already uses freely.

### Comments in JSON

The rules below govern the comments you write and the ones you edit. Leave a comment that predates them alone, unless you are changing it anyway. Rewrapping a configuration file for its own sake is not worth a diff.

- A comment line holds at most 80 characters of comment text. Count neither the indentation, nor the `//`, nor the space after it. Once a comment needs a second line, every line of it holds at most 72. The two limits differ on purpose: a comment just past 72 would otherwise spill three words onto a line of their own.
- A description takes one line, or two when unavoidable. A description names a setting. It is not its documentation. Anything longer belongs in a document.
- Never put JSON inside a comment. Nothing parses a commented-out member, so no tool can tell a stale one from a current one, and a reader cannot either. Record an omission in one line of prose instead: `// No "emptyChangelog": an empty changelog should stop us, not ship quietly.`
