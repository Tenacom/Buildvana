# Buildvana

Buildvana is an MSBuild-based build system. `.claude/rules/` holds the project instructions.

## Repository

- Upstream: `Tenacom/Buildvana`. Issues, PRs, and releases live there. It is the default target of `gh` calls.
- PR branches go to the contributor's own fork, the `origin` remote. Run `git remote -v` once per session when you need its name. Do not assume it.

## Rules index

The `.claude` directory is meant to be copied whole into other projects. This index says what survives the copy unchanged and what has to be rewritten on arrival. Each Markdown file under `.claude` says the same of itself in its first paragraph, so that the information survives a copy without this index. The rule files are split by the same criterion: `rules/common/` holds the portable ones, and `rules/project/` the project-specific ones. A file name is unique across both directories, so the rule files refer to each other by bare name.

### Portable: copy verbatim

- `rules/common/workflow.md`: how Ric and I work together: issues, PRs, the self-review cycle, sanity checks, out-of-scope fixes, commit messages.
- `rules/common/session.md`: scratchpad and handoff rules, for every session, with or without an issue.
- `skills/review-pr/SKILL.md`: how a PR is reviewed and how the review is posted. Run it with `/review-pr <N>`. Its "What the gate and CI already cover" section names `inspect.cs` and the workflows under `.github/workflows/`, and a copy names its own checks there. What a review does when a check fails comes from `rules/project/pr-checks.md`.
- `agents/pr-reviewer.md`: the subagent that runs the skill for the self-review cycle. Nothing repo-specific in it.
- `rules/common/design-principles.md`: scope, abstraction completeness, portability, conformance with the surrounding toolchain, LLM-automation stance.
- `rules/common/csharp-style-guide.md`: C# style beyond what `.editorconfig` and `.globalconfig` can express.
- `rules/common/dependency-injection.md`: service contracts, disposability as an implementation detail, and which services need a contract.
- `rules/common/file-formats.md`: encoding, indentation, and per-format conventions.
- `rules/common/powershell.md`: Windows PowerShell 5.1 pitfalls and shell-usage rules.
- `rules/common/testing.md`: test framework, MTP-only orchestration, coverage exclusion policy, cross-platform test rules.
- `rules/common/buildvana.md`: where the Buildvana documentation lives, the build commands, dependency updates, and which `bv` commands I may run. Assumes the project is built with Buildvana.
- `rules/common/dotnet.md`: efficient use of the .NET tooling, and how to read the source of a third-party package.
- `rules/common/documentation.md`: what counts as user documentation, the register, the structure of `docs/`, the changelog bullet format, and the checks. The names it relies on are in `rules/project/terminology.md`, and the inventory it applies to is in `rules/project/architecture.md`.
- `output-styles/simple-tech.md`: the register for every kind of prose, from chat to commit messages. Select it with `/output-style`.
- `tools/lint-commit.cs`: commit-message check, run on the draft before every commit. Its `bannedWords` and `announcingVerbs` arrays come from this repository's past commits. They apply anywhere, and a copy may extend them.
- `tools/inspect.cs`: the sanity-check gate. It finds the home directory and the solution file in it as `bv` does.
- `tools/lint-docs.cs`: documentation check, the first phase of `inspect.cs`. Its `fenceTags` array is the list in `rules/common/documentation.md`. Its `todoExemptFiles` and `sentenceExemptFiles` arrays are empty here, and a copy may name the files its TODO check and its sentence check leave alone.
- `scratchpad/`: scratch directory for temporary files, commit messages included. Its `Common.props` turns StyleCop and code style enforcement off for scratch code, and states every property it relies on. Everything else in the directory is gitignored.
- `settings.json`: the output style and the tool permissions that apply before the classifier. Nothing repo-specific in it.
- `.gitignore`: keeps `settings.local.json`, `worktrees/`, `agent-memory-local/`, `handoff/`, and the contents of `scratchpad/` out of git. Nothing repo-specific in it.

### Project-specific: rewrite when copied

- `rules/project/architecture.md`: Buildvana's own structure, project tiers, target platforms, self-hosting, tool portability, and the documentation inventory: files, subfolders of `docs/`, generated regions. Entirely about this repo.
- `rules/project/dependency-management.md`: the baseline dependencies, the `Buildvana.Runtime` strictly-BCL carve-out, the packages Buildvana SDK injects, the toolchain floors, and why the Buildvana family is never updated here. Entirely about this repo.
- `rules/project/terminology.md`: the one name for each thing the repository documents. Entirely about this repo.
- `rules/project/pr-checks.md`: the checks of this repository whose failure does not stop a PR review, and what a review does with each. Entirely about this repo.
- this file: repository coordinates, and the index itself.
