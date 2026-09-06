# Buildvana

Buildvana is an MSBuild-based build system. `.claude/rules/` holds the project instructions.

## Repository

- Upstream: `Tenacom/Buildvana`. Issues, PRs, and releases live there. It is the default target of `gh` and `mcp__github__*` calls.
- PR branches go to the contributor's own fork, the `origin` remote. Run `git remote -v` once per session when you need its name. Do not assume it.

## Rules index

The `.claude` directory is meant to be copied whole into other projects. This index says what survives the copy unchanged and what has to be rewritten on arrival.

### Portable: copy verbatim

- `rules/workflow.md`: how Ric and I work together: issues, PRs, reviews, sanity checks, out-of-scope fixes, commit messages.
- `rules/design-principles.md`: scope, abstraction completeness, portability, conformance with the surrounding toolchain, LLM-automation stance.
- `rules/csharp-style-guide.md`: C# style beyond what `.editorconfig` and `.globalconfig` can express.
- `rules/file-formats.md`: encoding, indentation, and per-format conventions, including the BOM workflow for new `.cs` files.
- `rules/powershell.md`: Windows PowerShell 5.1 pitfalls and shell-usage rules.
- `rules/testing.md`: test framework, MTP-only orchestration, coverage exclusion policy, cross-platform test rules.
- `rules/dotnet.md`: build commands and tooling. Assumes the project is built with Buildvana.
- `rules/documentation.md`: what counts as user documentation, the register, the structure of `docs/`, the changelog bullet format, and the checks. The names it relies on are in `rules/terminology.md`, and the inventory it applies to is in `rules/architecture.md`.
- `output-styles/simple-tech.md`: the register for every kind of prose, from chat to commit messages. Select it with `/output-style`.
- `templates/Default.cs`: new-file template carrying the BOM and the copyright preamble. The preamble names Tenacom. Change it for a project under different ownership.
- `tools/lint-commit.cs`: commit-message check, run on the draft before every commit. Its `bannedWords` and `announcingVerbs` arrays come from this repository's past commits. They apply anywhere, and a copy may extend them.
- `tools/lint-docs.cs`: documentation check, the first phase of `inspect.cs`. Its `fenceTags` array is the list in `rules/documentation.md`. Its `todoExemptFiles` array names this repository's root README, until #374 rewrites it, and a copy empties it.
- `scratchpad/`: scratch directory for temporary files, commit messages included. Created on first use.
- `settings.json`: MCP servers and tool permissions. Nothing repo-specific in it.
- `.gitignore`: keeps `settings.local.json`, `worktrees/`, `agent-memory-local/`, and `scratchpad/` out of git. Nothing repo-specific in it.

### Project-specific: rewrite when copied

- `rules/architecture.md`: Buildvana's own structure, project tiers, target platforms, self-hosting, tool portability, and the documentation inventory: files, subfolders of `docs/`, generated regions. Entirely about this repo.
- `rules/dependency-management.md`: mostly portable, but the baseline-dependency list and the `Buildvana.Runtime` strictly-BCL carve-out are Buildvana's own.
- `rules/terminology.md`: the one name for each thing the repository documents. Entirely about this repo.
- `tools/inspect.cs`: portable except one line: `const string SolutionFileName = "Buildvana.slnx"`. Change it, or the sanity-check gate fails on first run.
- this file: repository coordinates, and the index itself.
