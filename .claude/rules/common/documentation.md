# Documentation

This file is the same in every repository that copies `.claude`. A fact specific to one repository lives in another rule file.

User documentation is Markdown, rendered by the Git host. There is no separate site. The rules below say what counts as documentation, how it is written, and how it is checked. `file-formats.md` holds the Markdown format rules, such as emphasis markers and indentation. `output-styles/simple-tech.md` holds the register. `terminology.md` holds the name of each thing the repository documents. `rules/project/documentation.md` lists the repository's documentation files, its page kinds beyond the two of "Page kinds" below, the subfolders of `docs/`, the generated regions, the generated folders, and the pinned reference pages. `git-host.md` says how to identify the platform of a Git host, and "Git hosts" below says where the platforms render a page differently.

## What is documentation

The following files are technical documentation:

- `CHANGELOG.md`.
- Every file under `docs/`, except its `README.md` and the articles below. A file under a generated folder is documentation that a tool writes, and "Generated folders" below says which rules it follows.

The following are non-technical documentation:

- `README.md` files are either entry points, read before proper documentation, or mere indexes.
- `NuGet-README.md` files of packaged projects are entry points, too.
- Articles, under `docs/articles/`, aim for easy reading rather than formality.

Every other Markdown file is not documentation: third-party notices, analyzer release files, and the files under `.claude/`. The rules below do not apply to them.

## Register

`output-styles/simple-tech.md` applies to every **technical documentation** file. On top of it:

- Address the reader as "you". Name the product as the actor: "the SDK reads", "the tool writes".
- No first person. No "we", no "let's".
- No rhetorical questions, no jokes, no asides.
- One name per thing. `terminology.md` lists the names. When a thing has no name there, add one before using it in a second page.

**Non-technical documentation** is allowed a less formal register, because a visitor reads them before the documentation. Check it for accuracy and consistency, not for compliance to the rules above. `lint-docs.cs` holds the globs of these files, and `tools/ProjectFacts.cs` adds those of the repository, so that the sentence check leaves them alone. The other checks of `lint-docs.cs` apply to the files it reads: the root `README.md`, `docs/README.md`, the `NuGet-README.md` files, and the articles. A `README.md` elsewhere gets no check.

## Lines

- One sentence per line in prose. The Git host renders a single newline in a Markdown file as a space, so the rendering does not change. A review then diffs one sentence at a time, and the sentence length stays visible in the source.
- In a list item, a second sentence goes on a continuation line, indented to the item's text.
- A table cell is the exception, because a table row is one line. Where the automatic wrap would break a cell badly, a `<br>` tag breaks it by hand. markdownlint MD033 admits `br` for this reason.
- A line break inside a paragraph is two trailing spaces, as `file-formats.md` says. Use it only where the rendered text needs a break.

## Links

- Link a file in the repository with a relative path. A tag then links to the files of the tag. The `NuGet-README.md` files are the exception, because nuget.org renders them outside the repository.
- Link an external site with its full URL.
- Link a section with the anchor of its heading: the heading text in lowercase, with spaces as hyphens and punctuation removed. "Git hosts" below says where the platforms differ on punctuation. markdownlint MD051 checks an anchor within the same file, in GitHub's form. `lint-docs.cs` checks an anchor in another file, and the table of contents, in the form of the platform that `GitHost` in `tools/ProjectFacts.cs` names.
- Never link a page or a section that does not exist. The one exception is a page of a generated folder that the next release adds, as "Generated folders" says.
- Do not write `#1` for "number 1", and do not write `!1`. Both read as a reference to an issue, a pull request, or a merge request. Every platform links them in an issue, a pull request, a comment, or a release body, and "Git hosts" below says which platform links them in a file too. A reader takes them for a reference even where the platform does not link them.

## Pages describe the present

A page under `docs/` describes the code next to it. It does not say when a feature arrived, and it does not describe behavior that is gone. "Since", "new in", "previously", and "no longer" do not appear. A tag serves the pages of its version. The changelog is the version record.

A migration section is the exception. It describes the old behavior, because the reader is coming from it.

A page of a generated folder describes the latest release, as "Generated folders" says.

## The root README

The root README is the entry point. It holds what a visitor needs in order to decide whether to read on: what the product is, in two sentences, the packages with their badges, and links to the introduction, the getting-started page, and the index of `docs/`. It documents nothing itself.

## Structure of `docs/`

The rules of this section apply to the hand-written pages. A generated folder follows "Generated folders" below, and the index links it as that section says.

### The index

`docs/README.md` is the index. It links every hand-written page under `docs/`, with one line saying what the page covers. It has no table of contents, because it is one. Its shape:

- a title
- one paragraph
- one level 2 heading per group of pages, each holding a list of links

### File names

- Every file except `docs/README.md` has a kebab-case name.
- The name is a noun phrase for the topic: `getting-started.md`, `environment-variables.md`. It does not contain the product name.
- Subfolders go one level deep. A subfolder holds pages of one kind, and its name says the kind. `rules/project/documentation.md` lists them. A subfolder has no index of its own.

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

- An alert that applies to the whole page goes after the summary paragraph, before the first ruler. It is too important to blend into the prose. Several such alerts follow one another, each in its own block.
- A horizontal ruler precedes every level 2 heading, with a blank line above the ruler. Without the blank line, `---` turns the line above it into a heading.
- The table of contents lists every heading of level 2 and below, in document order. It does not list the title.
- Level 4 is the deepest heading. A page that needs level 5 is two pages.
- No "Overview" section. The summary paragraph above the table of contents does that job. Content an overview would hold goes in the first section, under a heading that names it.

### No stubs

A page exists when it has content. Do not commit a page holding "TODO", or a pointer to where the content will be. The index links only existing pages.

### Page kinds

A **guide** explains how something works, in prose, in the order the reader needs it.

A **reference** lists items of one kind: diagnostics, environment variables, properties, options. It has two shapes:

- A table, when every item fits one line per column, as a list of diagnostic ids with their messages does.
- One level 3 heading per item, named by the identifier in backticks, when an item needs paragraphs, as a list of environment variables does.

`rules/project/documentation.md` names the further page kinds of the repository, with the subfolder each sits in and the headings each has.

## Code blocks

- Every fenced block has a lowercase language tag from this list: `csharp`, `json`, `jsonc`, `markdown`, `powershell`, `shell`, `text`, `xml`, `yaml`.
- `shell` is for command lines, with no prompt prefix. `text` is for output and directory trees.
- Code starts at column zero. Do not indent an MSBuild snippet to show where it sits in a project file.
- A snippet shows the elements the reader adds, not the file around them. Show a whole file only when the file is the subject.

## Admonitions

Use alerts, in GitHub's syntax and with GitHub's meanings: `> [!NOTE]`, `> [!TIP]`, `> [!IMPORTANT]`, `> [!WARNING]`, `> [!CAUTION]`. Every platform renders them, as "Git hosts" below says. Write the type in uppercase, with nothing else on its line, because the platforms differ on both. Do not write a bold "NOTE:" inside a blockquote. An alert holds one paragraph.

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
- `rules/project/documentation.md` names each region and the tool that owns it.

## Generated folders

A generated folder is a subfolder of `docs/` that a tool writes in full at release. An API reference built from the XML comments of the code is one.

- `rules/project/documentation.md` names each generated folder, the tool that owns it, and which of its pages are the entry pages.
- Nothing edits a generated page by hand. A fix goes in the source the tool reads, such as an XML comment.
- The tool decides the names, the depth, and the shape of its pages. The file-name rules, the page template, the page kinds, and the sentence rules do not apply. `lint-docs.cs` skips the folder, and `.markdownlint-cli2.jsonc` lists it under `ignores`.
- The index links the entry pages of the folder, one line each. It does not link the other pages.
- The pages describe the latest release. On `main`, the page of a type that no release holds yet does not exist, and it arrives with the release that adds the type.
- A link into a generated folder is checked for its shape, and not for the existence of its target. A bullet under "Unreleased changes" links the page of a type whose release comes later, and `bv release` pins the link to the release tag, where the page exists.

## Changelog

`CHANGELOG.md` follows Keep a Changelog. `bv` owns the section skeleton: the "Unreleased changes" section, the release headings, and the four subsections "New features", "Changes to existing features", "Bugs fixed in this release", and "Known problems introduced by this release". The rules below govern what goes inside.

- A bullet is one sentence. No nested list, no second sentence.
- The bullet names what changed, with an identifier: a command, a property, a file, a diagnostic id.
- It does not say "now". Every bullet describes a change.
- The docs page that describes the feature is linked inline, on the phrase that names the feature. Do not add a trailing "See X.", which would be a second sentence.
- A bullet that names a type links the page of the type in the generated folder that holds the API reference, when the repository has one. The page may not exist until the release, as "Generated folders" says.
- A breaking change starts with `**BREAKING CHANGE**:` and names the behavior that is gone.
- Breaking changes come first in their subsection.
- Migration steps go in a section of the feature's page, and the bullet links there.
- A release section may open with one paragraph, when a note applies to the whole release. The sentence rules apply to it.
- A bullet under "Unreleased changes" links a repository file by relative path. When `bv release` moves the section into a release, it pins each such link to the release tag, so a released section keeps its links whatever happens to the files later. Do not edit links in a released section by hand.

Example:

```markdown
- **BREAKING CHANGE**: `Parser.Parse` no longer accepts a leading `+` sign.
- `Parser.TryParse` accepts [a thousands separator](docs/parsing.md#separators).
```

## Git hosts

`git-host.md` says how to identify the platform of a host. `rules/project/repository.md` names the hosts of the repository. GitHub, GitLab, and Forgejo render a Markdown file the same way, except for the facts below. Each fact comes from the documentation or the source of the platform.

### Anchors

Every platform lowercases the heading text, turns a space into a hyphen, and gives the second heading of a name the suffix `-1`, the third `-2`, and so on. Markdown formatting is not part of the text: the backticks of a code span go, and the text of a link stays while its target goes. The platforms differ on punctuation.

- GitHub keeps letters, digits, hyphens, and underscores, and drops every other character. `Louis.Hosting` becomes `louishosting`, and `a - b` becomes `a---b`.
- GitLab renders through comrak, whose anchors follow GitHub's rules. In a snippet, and not in a repository file, an anchor also gets a prefix from the file name.
- Forgejo keeps letters, digits, and underscores. A run of any other characters between two kept characters becomes one hyphen, and such a run at either end is dropped. `Louis.Hosting` becomes `louis-hosting`, and `a - b` becomes `a-b`.

`lint-docs.cs` computes the anchor of the platform that `GitHost` in `tools/ProjectFacts.cs` names. markdownlint MD051 knows GitHub's form alone, so a repository on Forgejo turns `link-fragments` off in `.markdownlint-cli2.jsonc` and leaves the anchors to `lint-docs.cs`.

### Alerts

Every platform renders the five alerts of "Admonitions" above, in GitHub's syntax.

- GitHub documents the five types in uppercase.
- GitLab renders them from version 17.10, documents them in lowercase, and takes text after the type on the marker line as the title of the alert. GitHub does not.
- Forgejo renders them from version 7.0, in any case. It also renders the older `**Note**` form, which no other platform does.

### References

A reference is a token such as `#1` that the platform turns into a link to an issue, a pull request, or a merge request.

- GitHub links `#1` in a conversation: an issue, a pull request, a comment, a release body. It links nothing in a repository file.
- GitLab links `#1` to an issue and `!1` to a merge request, in a conversation and in a repository file alike.
- Forgejo links `#1` and `!1` in a conversation. In a repository file, it links a cross-repository reference such as `owner/repo#1`, and leaves `#1` alone.

## Checks

- markdownlint runs with the configuration in `.markdownlint-cli2.jsonc`. `file-formats.md` says which rules it enforces.
- `.claude/tools/lint-docs.cs` runs as a phase of `inspect.cs --gate`. `Documentation.GeneratedFolders` in `tools/ProjectFacts.cs` names the generated folders, which it skips, and `GitHost` there names the platform whose anchors it computes. It checks:
  - file names under `docs/` are kebab-case, and subfolders are one level deep;
  - every page follows the template: summary paragraph, rulers, and a table of contents that matches the headings;
  - the index links every hand-written page, and no link points at a missing file or anchor, except a link into a generated folder;
  - every fenced block has a tag from the list above;
  - a prose line, outside a fenced block, a table, a released changelog section, and a file matching a glob of its `sentenceExemptFiles` array, holds one sentence of at most 25 words, counting a code span as one word and a link as the words of its text;
  - no documentation file contains "TODO";
  - generated-region markers exist and pair;
  - a bullet under "Unreleased changes" holds no nested list, and the breaking changes of a subsection come before its other bullets;
  - a released changelog section holds no relative file link.
- A reference page that lists a code surface is pinned by a test in the test project of that code. The test fails when an item exists in the code and not on the page, or on the page and not in the code. `rules/project/documentation.md` names the pinned pages, and the code each is pinned against.
