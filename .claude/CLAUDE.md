# Buildvana

Buildvana is an MSBuild-based build system. `.claude/rules/` holds the project instructions.

## Rules index

The `.claude` directory is meant to be copied whole into other projects. This index says what survives the copy unchanged and what has to be rewritten on arrival. Each Markdown file under `.claude` says the same of itself in its first paragraph, so that the information survives a copy without this index. The rule files are split by the same criterion: `rules/common/` holds the portable ones, and `rules/project/` the project-specific ones. A file name is unique across both directories, so the rule files refer to each other by bare name.

### Portable: copy verbatim

- `rules/common/workflow.md`: how Ric and I work together: issues, PRs, the self-review cycle, sanity checks, out-of-scope fixes, commit messages.
- `rules/common/session.md`: scratchpad and handoff rules, for every session, with or without an issue.
- `rules/common/design-principles.md`: scope, abstraction completeness, portability, conformance with the surrounding toolchain, LLM-automation stance.
- `rules/common/csharp-style-guide.md`: C# style beyond what `.editorconfig` and `.globalconfig` can express.
- `rules/common/dependency-injection.md`: disposability as an implementation detail, and when a service gets a contract.
- `rules/common/file-formats.md`: encoding, indentation, and per-format conventions.
- `rules/common/powershell.md`: Windows PowerShell 5.1 pitfalls and shell-usage rules.
- `rules/common/testing.md`: test framework, MTP-only orchestration, coverage exclusion policy, cross-platform test rules.
- `rules/common/buildvana.md`: build commands, dependency commands, and the Buildvana documentation. Assumes the project is built with Buildvana.
- `rules/common/dotnet.md`: efficiency rules for .NET tooling, and how to inspect a third-party package.
- `rules/common/git-host.md`: what the Git host is, the three platforms, and how to identify the platform and the version of a host. The hosts of the repository are in `rules/project/repository.md`.
- `rules/common/documentation.md`: what counts as user documentation, the register, the structure of `docs/`, the changelog bullet format, the rendering differences between the platforms, and the checks. The names it relies on are in `rules/project/terminology.md`, and the inventory it applies to is in `rules/project/documentation.md`.
- `skills/review-pr/SKILL.md`: how a PR is reviewed and how the review is posted. Run it with `/review-pr <N>`. Its "What the gate and CI already cover" section names `inspect.cs` and the workflows under `.github/workflows/`, and a copy names its own checks there.
- `agents/pr-reviewer.md`: the subagent that runs the skill for the self-review cycle. Nothing repo-specific in it.
- `output-styles/simple-tech.md`: the register for every kind of prose, from chat to commit messages. Select it with `/output-style`.
- `tools/Common.props`: build options for the tools, and the `Compile` item that puts `tools/ProjectFacts.cs` into every tool. The `.editorconfig` next to it turns off the StyleCop rules that a single-file app cannot follow, SA1636 included, so that a tool keeps its header whatever copyright text the repository sets.
- `tools/inspect.cs`: the sanity-check gate. It finds the solution file in the home directory by itself.
- `tools/lint-commit.cs`: commit-message check, run on the draft before every commit. Its `bannedWords` and `announcingVerbs` arrays come from past commits of the repository the tool was written in. They apply anywhere, and `tools/ProjectFacts.cs` extends them.
- `tools/lint-docs.cs`: documentation check, the first phase of `inspect.cs`. Its `fenceTags` array is the list in `rules/common/documentation.md`, and its `sentenceExemptFiles` array holds the non-technical documentation that file lists. The globs it adds to either check, the generated folders, and the platform of the Git host come from `tools/ProjectFacts.cs`.
- `scratchpad/`: scratch directory for temporary files, commit messages included. Its `Common.props` turns StyleCop and code style enforcement off for scratch code, and states every property it relies on. Everything else in the directory is gitignored.
- `settings.json`: the output style and the tool permissions that apply before the classifier. Nothing repo-specific in it.
- `.gitignore`: keeps `settings.local.json`, `worktrees/`, `agent-memory-local/`, `handoff/`, and the contents of `scratchpad/` out of git. Nothing repo-specific in it.

### Project-specific: rewrite when copied

- `rules/project/repository.md`: the upstream repository, the fork remote, the hosts and their platforms, and CI. Entirely about this repo.
- `rules/project/architecture.md`: Buildvana's own structure, project tiers, target platforms, self-hosting, and tool portability. Entirely about this repo.
- `rules/project/documentation.md`: the documentation inventory: files, page kinds beyond guide and reference, subfolders of `docs/`, generated regions, generated folders, pinned reference pages. Entirely about this repo.
- `rules/project/dependency-management.md`: the baseline dependencies, the `Buildvana.Runtime` strictly-BCL carve-out, the packages Buildvana SDK injects, the toolchain floors, and why the Buildvana family is never updated here. Entirely about this repo.
- `rules/project/terminology.md`: the one name for each thing the repository documents. Entirely about this repo.
- `rules/project/pr-checks.md`: the checks of a pull request whose failure does not stop a review. The Codecov section applies to any repository whose CI uploads to Codecov, and a copy adds its own checks.
- `rules/project/people.md`: who holds each role the rule files name. Entirely about this repo.
- `tools/ProjectFacts.cs`: the facts the tools read. The platform of the Git host sits at the top. `Documentation` holds the facts of `lint-docs.cs`: the generated folders, and the globs the TODO and sentence checks leave alone. `CommitMessages` holds the words `lint-commit.cs` rejects on top of its own lists. Entirely about this repo.
- this file: the index itself.
