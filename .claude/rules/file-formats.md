# File formats

File format rules are configured in `.editorconfig`.
Here are additional rules that either cannot be codified there, or are better known in advance.

## Common defaults for all files (valid unless otherwise specified below)

- Charset: UTF-8 without BOM
- Line separator: LF
- Indentation: spaces (NOT tabs)
- Tab width = indentation width = 4
- MUST have a trailing blank line

## C# source files (`*.cs`)

- Charset: UTF-8 with BOM. StyleCop's SA1412 hard-fails the build on missing BOM.

### Creating new C# files

The `Write` tool silently strips the leading BOM (even when U+FEFF is embedded in the content), which makes it unfit for creating new `.cs` files. Use this workflow instead:

1. Copy `.claude/templates/Default.cs` to the target path. The template carries the BOM and the standard copyright preamble.
2. Use the `Edit` tool to replace `// __EVERYTHING_GOES_HERE__` with the file body. `Edit` preserves the BOM.

If you must use `Write` to fully rewrite an existing `.cs` file (which also strips the BOM), prepend `0xEF 0xBB 0xBF` to the file afterwards.

## MSBuild XML files (`*.*proj`, `*.props`, `*.targets`)

- NO prolog (`<?xml ... ?>`)
- Tab width = indentation width = 2

## Other XML files

- Prolog (`<?xml ... ?>`) usually required

## Markdown files (`*.md`)

- Tab width = indentation width = 2
- Markdown line break: 2 spaces
- Always use `_` for emphasis, `**` for strong emphasis. Applies to all `.md` files, including AI-consumed ones — markdownlint rule MD049 is a backup enforcement, not the source of the rule.  
  Example: `_emphasis_` and `**strong emphasis**` are correct; `*emphasis*` or `__strong emphasis__` are NOT correct.

Generally honor markdownlint rules laid out in `.markdownlint-cli2.jsonc`. Only when absolutely necessary, suppress rules with XML comments. Example:

```markdown
<!-- markdownlint-disable MD036 -->
**This line will not be flagged as using emphasis as heading**
<!-- markdownlint-enable MD036 -->
```

Markdown files consumed by AIs (e.g., `CLAUDE.md` and files in `.claude`) are exempt from markdownlint rules.

## JSON files (`*.json`, `*.jsonc`, `*.json5`)

- Tab width = indentation width = 2
- Use comments in `.jsonc` files, JSON5 features in `.json5` files.
- Do NOT use comments or JSON5 features in `.json` files, unless instructed to do so, or if they are already used in the file. Some tools consume `.json` files but support comments and/or JSON5 features in them; do not assume this is the case, but use already-used features liberally.
