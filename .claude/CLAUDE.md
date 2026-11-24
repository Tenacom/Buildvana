# Buildvana

Buildvana is an MSBuild-based build system.
See `.claude/rules/` for project instructions.

## What a copy of `.claude` keeps

The `.claude` directory is meant to be copied whole into other projects. This section says what survives the copy unchanged and what has to be rewritten on arrival. Each Markdown file under `.claude` says the same of itself in its first paragraph, so that the information survives a copy without this section.

### Rule files

`rules/common/` holds the portable rule files, and `rules/project/` the project-specific ones. A file name is unique across both directories, so the rule files refer to each other by bare name.

In `rules/project/pr-checks.md`, the Codecov section applies to any repository whose CI uploads to Codecov.

### Host files

Every file under `hosts/` is portable. "Host files" in `rules/common/git-host.md` says what they hold, and which one a session reads.

### Portable: copy verbatim

- `skills/review-pr/SKILL.md`: how a PR is reviewed and how the review is posted. Run it with `/review-pr <N>`. It names `inspect.cs`, and takes the CI from `rules/project/repository.md`, the checks to tolerate from `rules/project/pr-checks.md`, and every command on the host from the host file. Nothing repo-specific in it.
- `agents/pr-reviewer.md`: the subagent that runs the skill for the self-review cycle. Nothing repo-specific in it.
- `output-styles/simple-tech.md`: the register for every kind of prose, from chat to commit messages. Select it with `/output-style`.
- `output-styles/informal-tech.md`: the register of an informal document, as "Register" in `rules/common/documentation.md` defines it. `settings.json` does not select it, so a session reads it before it writes such a document.
- `tools/Common.props`: build options for the tools, and the `Compile` item that puts `tools/ProjectFacts.cs` into every tool. The `.editorconfig` next to it turns off the StyleCop rules that a single-file app cannot follow, SA1636 included, so that a tool keeps its header whatever copyright text the repository sets.
- `tools/inspect.cs`: the sanity-check gate. It finds the solution file in the home directory by itself, and runs the generator of the documentation site when the home directory holds `docgen.json`.
- `tools/lint-commit.cs`: commit-message check, run on the draft before every commit. Its `bannedWords` and `announcingVerbs` arrays come from past commits of the repository the tool was written in. They apply anywhere, and `tools/ProjectFacts.cs` extends them.
- `tools/lint-docs.cs`: documentation check, the first phase of `inspect.cs`. Its `fenceTags` array is the list in `rules/common/documentation.md`, and its `informalDocuments` array holds the informal documents that file names. The globs of the files the TODO check leaves alone, the globs of the other informal documents, the URL of the documentation site, and the platform of the Git host come from `tools/ProjectFacts.cs`.
- `scratchpad/`: scratch directory for temporary files, commit messages included. Its `Common.props` turns StyleCop and code style enforcement off for scratch code, and states every property it relies on. Everything else in the directory is gitignored.
- `settings.json`: the output style and the tool permissions that apply before the classifier. Nothing repo-specific in it.
- `.gitignore`: keeps `settings.local.json`, `worktrees/`, `agent-memory-local/`, `handoff/`, and the contents of `scratchpad/` out of git. Nothing repo-specific in it.

### Project-specific: rewrite when copied

- `tools/ProjectFacts.cs`: the facts the tools read. The platform of the Git host sits at the top. `Documentation` holds the facts of `lint-docs.cs`: the URL of the documentation site, the globs the TODO check leaves alone, and the globs of the informal documents. `CommitMessages` holds the words `lint-commit.cs` rejects on top of its own lists. Entirely about this repo.
- this file.
