# Documentation

User documentation is Markdown, rendered by GitHub. There is no separate site. The rules below say what counts as documentation, how it is written, and how it is checked. `file-formats.md` holds the Markdown format rules, such as emphasis markers and indentation. `output-styles/simple-tech.md` holds the register. `terminology.md` holds the name of each thing the repository documents. The "Documentation" section of `architecture.md` lists the repository's documentation files, the subfolders of `docs/`, and the generated regions.

## What is documentation

- `README.md` at the repository root.
- `CHANGELOG.md`.
- Every file under `docs/`.
- The `NuGet-README.md` of each packaged project. nuget.org renders it. It points at the root README and holds no documentation of its own.

Every other Markdown file is not documentation: third-party notices, analyzer release files, and the files under `.claude/`. The rules below do not apply to them.

## Register

`output-styles/simple-tech.md` applies to every documentation file. On top of it:

- Address the reader as "you". Name the product as the actor: "Buildvana SDK reads", "`bv` writes".
- No first person. No "we", no "let's".
- No rhetorical questions, no jokes, no asides.
- One name per thing. `terminology.md` lists the names. When a thing has no name there, add one before using it in a second page.

## Lines

- One sentence per line in prose. GitHub renders a single newline in a Markdown file as a space, so the rendering does not change. A review then diffs one sentence at a time, and the sentence length stays visible in the source.
- In a list item, a second sentence goes on a continuation line, indented to the item's text.
- A table cell is the exception, because a table row is one line. Where the automatic wrap would break a cell badly, a `<br>` tag breaks it by hand. markdownlint MD033 admits `br` for this reason.
- A line break inside a paragraph is two trailing spaces, as `file-formats.md` says. Use it only where the rendered text needs a break.

## Links

- Link a file in the repository with a relative path. A tag then links to the files of the tag. The `NuGet-README.md` files are the exception, because nuget.org renders them outside the repository.
- Link an external site with its full URL.
- Link a section with GitHub's anchor form: lowercase, punctuation removed, spaces as hyphens. markdownlint MD051 checks an anchor within the same file. `lint-docs.cs` checks an anchor in another file.
- Never link a page or a section that does not exist.
- Do not write `#1` for "number 1". It reads as an issue reference. GitHub links it in an issue, a pull request, a comment, or a release body, and a reader takes it for one even in a file, where GitHub does not.

## Pages describe the present

A page under `docs/` describes the code next to it. It does not say when a feature arrived, and it does not describe behavior that is gone. "Since", "new in", "previously", and "no longer" do not appear. A tag serves the pages of its version. The changelog is the version record.

A migration section is the exception. It describes the old behavior, because the reader is coming from it.

## The root README

The root README is the entry point. It holds what a visitor needs in order to decide whether to read on: what the product is, in two sentences, the packages with their badges, and links to the introduction, the getting-started page, and the index of `docs/`. It documents nothing itself.

## Structure of `docs/`

### The index

`docs/README.md` is the index. It links every other page under `docs/`, with one line saying what the page covers. It has no table of contents, because it is one. Its shape:

- a title
- one paragraph
- one level 2 heading per group of pages, each holding a list of links

### File names

- Every file except `docs/README.md` has a kebab-case name.
- The name is a noun phrase for the topic: `hooks.md`, `sdk-diagnostics.md`. It does not contain the product name.
- Subfolders go one level deep. A subfolder holds pages of one kind, and its name says the kind. The "Documentation" section of `architecture.md` lists them. A subfolder has no index of its own.

### Page template

Every page except `docs/README.md` follows this template:

```markdown
# Title

One to three sentences saying what the page covers. A reader arriving from a link reads them before the table of contents.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Level 2 heading](#level-2-heading)
  - [Level 3 heading](#level-3-heading)

---

## Level 2 heading

Text.

### Level 3 heading

Text.
```

- A horizontal ruler precedes every level 2 heading, with a blank line above the ruler. Without the blank line, `---` turns the line above it into a heading.
- The table of contents lists every heading of level 2 and below, in document order. It does not list the title.
- Level 4 is the deepest heading. A page that needs level 5 is two pages.
- No "Overview" section. The summary paragraph above the table of contents does that job. Content an overview would hold goes in the first section, under a heading that names it.

### No stubs

A page exists when it has content. Do not commit a page holding "TODO", or a pointer to where the content will be. The index links only existing pages.

### Page kinds

A **guide** explains how something works, in prose, in the order the reader needs it.

A **reference** lists items of one kind: diagnostics, environment variables, properties, options. It has two shapes:

- A table, when every item fits one line per column. The diagnostics pages are the model.
- One level 3 heading per item, named by the identifier in backticks, when an item needs paragraphs. The environment variables page is the model.

A **module page** documents one SDK module. Its level 2 headings come in this order, each present when the module has the content: "Configuration", for the properties the user sets; "Usage", for items, tasks, and targets; "Diagnostics", for a link to the module's range in the SDK diagnostics page.

A **command page** documents one `bv` command or command group. Topic sections come first. "Options" and "Exit codes" come last, each present when the command has them.

## Code blocks

- Every fenced block has a lowercase language tag from this list: `csharp`, `json`, `jsonc`, `markdown`, `powershell`, `shell`, `text`, `xml`, `yaml`.
- `shell` is for command lines, with no prompt prefix. `text` is for output and directory trees.
- Code starts at column zero. Do not indent an MSBuild snippet to show where it sits in a project file.
- A snippet shows the elements the reader adds, not the file around them. Show a whole file only when the file is the subject.

## Admonitions

Use GitHub alerts, with GitHub's meanings: `> [!NOTE]`, `> [!TIP]`, `> [!IMPORTANT]`, `> [!WARNING]`, `> [!CAUTION]`. Do not write a bold "NOTE:" inside a blockquote. An alert holds one paragraph.

## Emoji and glyphs

A word carries the meaning, and an emoji may decorate it. A list that says "supported" with a heart and "unsupported" with a thumb down says nothing to a reader without the glyphs. "Glyphs never carry meaning alone" in `design-principles.md` states the rule for the console. It holds here.

## Generated regions

A region a tool maintains sits between two HTML comment markers:

```markdown
<!-- REGION-NAME:START -->
...
<!-- REGION-NAME:END -->
```

- The tool rewrites everything between the markers, and its check mode reports drift.
- Nothing edits the region by hand. The prose around it is hand-written.
- The "Documentation" section of `architecture.md` names each region and the tool that owns it.

## Changelog

`CHANGELOG.md` follows Keep a Changelog. `ChangelogUpdater` in `bv` owns the section skeleton: the "Unreleased changes" section, the release headings, and the four subsections "New features", "Changes to existing features", "Bugs fixed in this release", and "Known problems introduced by this release". The rules below govern what goes inside.

- A bullet is one sentence. No nested list, no second sentence.
- The bullet names what changed, with an identifier: a command, a property, a file, a diagnostic id.
- It does not say "now". Every bullet describes a change.
- The docs page that describes the feature is linked inline, on the phrase that names the feature. Do not add a trailing "See X.", which would be a second sentence.
- A breaking change starts with `**BREAKING CHANGE**:` and names the behavior that is gone.
- Breaking changes come first in their subsection.
- Migration steps go in a section of the feature's page, and the bullet links there.
- A release section may open with one paragraph, when a note applies to the whole release. The sentence rules apply to it.

Example:

```markdown
- **BREAKING CHANGE**: `bv` no longer reads `CONFIGURATION`, `VERSION_SPEC_CHANGE`, `CHECK_PUBLIC_API_FILES`, or `UPDATE_SELF_REFERENCES` as defaults for its options.
- When `.config/dotnet-tools.json` pins `bv`, the pinned version [runs in place of the invoked one](docs/directory-structure.md#configdotnet-toolsjson).
```

## Checks

- markdownlint runs with the configuration in `.markdownlint-cli2.jsonc`. `file-formats.md` says which rules it enforces.
- `.claude/tools/lint-docs.cs` runs as a phase of `inspect.cs --gate`. It checks:
  - file names under `docs/` are kebab-case, and subfolders are one level deep;
  - every page follows the template: summary paragraph, rulers, and a table of contents that matches the headings;
  - the index links every page, and no link points at a missing file or anchor;
  - every fenced block has a tag from the list above;
  - no documentation file contains "TODO";
  - generated-region markers exist and pair;
  - a changelog bullet holds no nested list, and no sentence break outside code spans.
