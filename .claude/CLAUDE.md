# Buildvana

This is Buildvana, a MSBuild-based build system.
See `.claude/rules/` for project instructions.

## Repository

- Upstream: `Tenacom/Buildvana` — issues, PRs, and releases live here. Default target for `gh` and `mcp__github__*` calls.
- PR branches are pushed to the contributor's own fork, which is the `origin` remote. Run `git remote -v` once per session if you need its name; do not assume it.

## Rules index

The `.claude` directory is meant to be copied wholesale into other projects. This index says what survives the copy unchanged and what has to be rewritten on arrival.

### Portable — copy verbatim

- `rules/workflow.md` — how Ric and I work together: issues, PRs, reviews, sanity checks, out-of-scope fixes.
- `rules/design-principles.md` — scope, abstraction completeness, portability, conformance with the surrounding toolchain, LLM-automation stance.
- `rules/csharp-style-guide.md` — C# style beyond what `.editorconfig` and `.globalconfig` can express.
- `rules/file-formats.md` — encoding, indentation, and per-format conventions (including the BOM workflow for new `.cs` files).
- `rules/powershell.md` — Windows PowerShell 5.1 pitfalls and shell-usage rules.
- `rules/testing.md` — test framework, MTP-only orchestration, coverage exclusion policy, cross-platform test rules.
- `rules/dotnet.md` — build commands and tooling. Assumes the project is built with Buildvana.
- `rules/nuget-version-lookup.md` — procedure for resolving a package's target version.
- `templates/Default.cs` — new-file template carrying the BOM and the copyright preamble. The preamble names Tenacom; change it for a project under different ownership.
- `tools/lint-commit.cs` — commit-message check, run on the draft before every commit. Nothing repo-specific in it.
- `settings.json` — MCP servers and tool permissions. Nothing repo-specific in it.

### Project-specific — rewrite when copied

- `rules/architecture.md` — Buildvana's own structure, project tiers, target platforms, self-hosting, tool portability. Entirely about this repo.
- `rules/dependency-management.md` — mostly portable, but the baseline-dependency list and the `Buildvana.Runtime` strictly-BCL carve-out are Buildvana's own.
- `tools/inspect.cs` — portable except one line: `const string SolutionFileName = "Buildvana.slnx"`. Change it, or the sanity-check gate fails on first run.
- this file — repository coordinates, and the index itself.
