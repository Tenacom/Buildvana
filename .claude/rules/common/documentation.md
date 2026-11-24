# Documentation

This file is the same in every repository that copies `.claude`. A fact specific to one repository lives in another rule file.

All user documentation is Markdown. The Git host renders every documentation file outside `docs/`, such as a `README.md` file and the changelog. In a repository with a documentation site, the site renders the files under `docs/`, as "Documentation site" below says. In every other repository, the Git host renders them too. The rules below say what counts as documentation, how it is written, and how it is checked. `file-formats.md` holds the Markdown format rules, such as emphasis markers and indentation. `output-styles/simple-tech.md` and `output-styles/informal-tech.md` hold the two registers, and "Register" below says which one a file follows. `terminology.md` holds the name of each thing the repository documents. `rules/project/documentation.md` lists the repository's documentation files, its informal documents, its page kinds beyond the two of "Page kinds" below, the subfolders of `docs/`, the generated regions, the published documentation, and the pinned reference pages. `git-host.md` says how to identify the platform of a Git host, and "Git hosts" below says where the platforms render a page differently.

## What is documentation

The following files are documentation:

- `README.md` files. The root one is the entry point, read before the rest of the documentation. "The indexes" below covers a `README.md` under `docs/`.
- `NuGet-README.md` files of packaged projects, which are entry points too.
- `CHANGELOG.md`.
- Every Markdown file under `docs/`.
- The API reference, in a repository that has one. A tool generates it from the XML comments of the code, so it is no file of the repository. "API reference" below says which rules it follows.

Every other Markdown file is not documentation: third-party notices, analyzer release files, the community files `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, and `SECURITY.md` where present, and the files under `.claude/`. The rules below do not apply to them.

## Register

A documentation file follows one of two registers. `output-styles/simple-tech.md` holds the default register. `output-styles/informal-tech.md` holds the register of an informal document.

A file is an informal document in each of these cases:

- Its name is `README.md` or `NuGet-README.md`.
- Its YAML front matter has the property `x-informal`, set to `true`.
- `rules/project/documentation.md` lists it as an informal document.

Claude Code loads `simple-tech.md` alone, as the output style that `settings.json` selects. Read `informal-tech.md` before you write or edit an informal document.

On top of `simple-tech.md`, a file in the default register follows these rules:

- Address the reader as "you". Name the product as the actor: "the SDK reads", "the tool writes".
- No first person. No "we", no "let's".
- No rhetorical questions, no jokes, no asides.

In both registers, a thing has one name. `terminology.md` lists the names. When a thing has no name there, add one before using it in a second page.

`lint-docs.cs` runs its sentence check on the files in the default register alone. It knows an informal document by its name and by its front matter, and `tools/ProjectFacts.cs` holds the globs of the ones the repository lists. The other checks of `lint-docs.cs` apply to every file it reads: the root `README.md`, `CHANGELOG.md`, the `NuGet-README.md` files, and the files under `docs/`. A `README.md` elsewhere gets no check.

## Lines

- One sentence per line in prose, in both registers. The Git host renders a single newline in a Markdown file as a space, so the rendering does not change. A review then diffs one sentence at a time, and the sentence length stays visible in the source.
- In a list item, a second sentence goes on a continuation line, indented to the item's text.
- A table cell is the exception, because a table row is one line. Where the automatic wrap would break a cell badly, a `<br>` tag breaks it by hand. markdownlint MD033 admits `br` for this reason.
- A line break inside a paragraph is two trailing spaces, as `file-formats.md` says. Use it only where the rendered text needs a break.

## Links

- Link a file in the repository with a relative path. A tag then links to the files of the tag. The `NuGet-README.md` files are an exception, because nuget.org renders them outside the repository. The pages of a documentation site are another, as "Documentation site" below says.
- Link an external site with its full URL.
- Link a section with the anchor of its heading: the heading text in lowercase, with spaces as hyphens and punctuation removed. "Git hosts" below says where the platforms differ on punctuation. markdownlint MD051 checks an anchor within the same file, in GitHub's form. `lint-docs.cs` checks an anchor in another file, and the table of contents, in the form of the platform that `GitHost` in `tools/ProjectFacts.cs` names.
- Never link a page or a section that does not exist. The one exception is a page of the API reference that the next release adds, as "Documentation site" below says.
- Do not write `#1` for "number 1", and do not write `!1`. Both read as a reference to an issue, a pull request, or a merge request. Every platform links them in an issue, a pull request, a comment, or a release body, and "Git hosts" below says which platform links them in a file too. A reader takes them for a reference even where the platform does not link them.

## Pages describe the present

A page under `docs/` describes the code next to it. It does not say when a feature arrived, and it does not describe behavior that is gone. "Since", "new in", "previously", and "no longer" do not appear. A tag serves the pages of its version. The changelog is the version record.

A migration section is the exception. It describes the old behavior, because the reader is coming from it.

## The root README

The root README is the entry point. It holds what a visitor needs in order to decide whether to read on: what the product is, in two sentences, the packages with their badges, and links to the introduction, the getting-started page, and the index of `docs/`. In a repository with a documentation site, it links the site in place of those pages. It documents nothing itself.

## Structure of `docs/`

A repository may have no `docs/` directory. In a repository with a documentation site, "Documentation site" below replaces "The indexes" and "Page template". The other subsections hold in every repository.

### The indexes

A `README.md` under `docs/` is the index of its directory. The Git host shows it to a reader who opens the directory.

- `docs/` has an index, and so does every folder under it that holds a page, at any depth.
- An index links every page of its directory, and the index of every subfolder that has one. Each link has one line saying what the page or the subfolder covers.
- An index has no table of contents, because it is one. Its shape:
  - a title
  - one paragraph
  - a list of links, or one level 2 heading per group of links

### File names

- Every file except a `README.md` has a kebab-case name.
- The name is a noun phrase for the topic: `getting-started.md`, `environment-variables.md`. It does not contain the product name.
- A subfolder holds pages of one kind, and its name says the kind. `rules/project/documentation.md` lists the subfolders. They nest to any depth.

### Page template

Every page except an index follows this template. `rules/project/documentation.md` lists the other pages that do not follow it, when the repository has any. In a page with a front matter, the template starts after it.

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

A page exists when it has content. Do not commit a page holding "TODO", or a pointer to where the content will be. An index links only existing pages.

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

## Published documentation

A repository has a documentation site when its home directory holds `docgen.json`. Without that file, this section does not apply, and no rule elsewhere that names the documentation site or the API reference does.

- Meziantou.DocumentationGenerator builds the site. It renders every Markdown file under `docs/` as a page, and it builds the API reference, when the repository has one.
- The workflow `docs.yml` publishes the site. The `docs` job of the release workflow calls it after `bv release`, and a maintainer runs it by hand to publish again.
- A public repository on `github.com` has its site on GitHub Pages, at `https://<owner>.github.io/<repo>/`.
- The site of a private repository has no host yet. Ask the maintainer before you add `docgen.json` to a private repository.
- `rules/project/documentation.md` names the URL of the site.

### API reference

A repository with a documentation site has an API reference when the `packages` property of `docgen.json` names a package. The property may be empty or absent, and the site then holds the pages under `docs/` alone. The generator builds the API reference from the XML comments of the packages that the property names. The repository holds none of its pages.

- An entry of `packages` has the `id` of a package, the `version` `*-*`, and the `path` `artifacts/Release`, the folder where `bv pack` leaves the packages. The version `*-*` selects the highest version in a folder, prereleases included.
- A package that `packages` does not name gets no page.
- Nothing edits a page of the API reference by hand. A fix goes in the XML comment the page comes from.
- "XML comments" in `csharp-style-guide.md` governs the text of a page. The page template, the page kinds, and the sentence rules do not apply.

### Documentation site

- `docgen.json` holds what every build of the site shares, such as the site title and the packages of the API reference.
- The site has two parts. The stable part describes the stable release with the highest version. The preview part describes the release with the highest version, stable or prerelease.
- `docs.yml` builds both parts from the assets of those two releases, and deploys the site. For each part, it reads the packages that `docgen.json` names from the assets of the release. No branch holds a generated page.
- A part describes one release. The page of a type that no release holds yet does not exist, and it arrives with the release that adds the type.
- A link to a page of the site, from a file that the Git host renders, is a full URL, and no tool checks that the page exists.
- The preview part is the `preview` folder of the stable part, so its URL is the URL of the site followed by `preview/`.
- A warning of the generator is a defect of an XML comment, such as a `cref` that names no member, or of a page of the site. "Checks" below says where the generator runs.

#### Structure of the site

- The `content` section of `docgen.json` names `docs` as its `root`.
- `docs/index.md` is the home page of the site, and it is required. The repository has no `docs/README.md`, because the site has a navigation and needs no index.
- The home page says what the product is, as the root README does. Where the site has an API reference, the `<PackageList />` component of the generator lists the packages there.
- Each part of the site takes `docgen.json` and the pages from the tag of its release. A change to a page therefore reaches a part with the next release that the part describes.
- A subfolder that holds pages has a `_folder.yml` file with a `title`. The generator names the folder by that title in the navigation.
- No folder takes the path `preview` in the site, because the preview part is the `preview` folder of the stable part. A folder takes its path from the `slug` of its `_folder.yml`, or else from its title.
- A number prefix of two digits, such as `01-`, sets the position of a page or of a folder in the navigation, because the generator sorts the entries of a folder by name. The numbers of a folder start at `01` and have no gap. `sortOrder` is not used, so that the order of the files in a directory listing is the order of the navigation.
- A folder whose pages sort by name alone, such as a folder of reference pages, has no prefix on its pages.
- The prefix reaches no URL, because the generator takes the URL of a page and of a folder from its title. It is not part of the name that "File names" above governs.
- A file that moves or gets another number changes its path. The same change updates every link to the file: in the pages, under "Unreleased changes" in the changelog, in the rule files, and in the code.

#### Pages of the site

- The title of a page is `title` in a YAML front matter, or a level 1 heading on the first line. A summary paragraph of one to three sentences follows the title.
- The generator ignores a property of the front matter that it does not know, such as `x-informal`.
- A page has no ruler before a heading, and no table of contents. The site lists the headings of a page by itself.
- The rules of the page template on alerts, on the depth of headings, and on an "Overview" section hold for a page of the site.
- The generator takes the URL of a page from its title, and not from its file name. When the title of a published page changes, `slug` in the front matter keeps the URL.
- A link to another page is a relative path to its Markdown file.
- A link to a type, a member, or a namespace of the API reference is a `cref:` link, such as `[the parser](cref:T:<namespace>.<type>)`.
- A link to a repository file outside `docs/` is a full URL, because the site publishes nothing outside its content root.
- The generator gives a heading the anchor of GitHub's rules, on every Git host.
- A page may hold a component of the generator, such as `<PackageList />`. `.markdownlint-cli2.jsonc` admits the components under MD033.

## Changelog

`CHANGELOG.md` follows Keep a Changelog. `bv` owns the section skeleton: the "Unreleased changes" section, the release headings, and the four subsections "New features", "Changes to existing features", "Bugs fixed in this release", and "Known problems introduced by this release". The rules below govern what goes inside.

- The reader of a bullet is a person who upgrades to the release. The bullet says what that reader gets or loses. It does not summarize the PR, and it does not say how the change is produced: no hook, no tool, no source file, unless the reader acts on it.
- A bullet is one sentence. No nested list, no second sentence.
- The bullet names what changed, with an identifier: a command, a property, a file, a diagnostic id.
- A new type gets one bullet when the type is the feature, as a type that runs a child process is. The methods of a utility class get a bullet each, because a reader may need one of them alone. 
- A method shares its bullet with its asynchronous counterpart, and with the same method for another type, such as a string and a character span.
- - A type or member that exists only to support another gets no bullet. Given a new `Nutcracker` type, its contract `INutcracker` and its `NutcrackerWorkingMode` enum get none. The bullet of `Nutcracker` may name them.
- A bullet about a member says what the member does, and leaves out the condition under which the member acts. The condition and the edge cases belong in the documentation of the member. Write "`Trim` removes white space from both ends of a string", not "of a string that is not empty".
- It does not say "now". Every bullet describes a change.
- The docs page that describes the feature is linked inline, on the phrase that names the feature. Do not add a trailing "See X.", which would be a second sentence.
- In a repository with a documentation site, that link still names the Markdown file, and not the page of the site. The tag of the release keeps the file, and a later build of the site may drop the page.
- No bullet links the documentation site, the API reference included. A bullet names a type or a member in a code span, without a link, because a later build of the site may drop its page.
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
- `.claude/tools/lint-docs.cs` runs as a phase of `inspect.cs --gate`. `GitHost` in `tools/ProjectFacts.cs` names the platform whose anchors it computes, and `Documentation.SiteUrl` there holds the URL of the documentation site, or an empty string when there is none. A member of the documentation set that does not exist, such as `docs/` or `CHANGELOG.md`, is skipped and named on standard error, so that a repository without documentation passes. It checks:
  - file names under `docs/` are kebab-case;
  - every page follows the template: summary paragraph, rulers, and a table of contents that matches the headings;
  - every folder that holds a page has an index, and an index links every page of its directory and the index of every subfolder;
  - no link points at a missing file or anchor;
  - every fenced block has a tag from the list above;
  - a prose line, outside a fenced block, a table, a released changelog section, and an informal document, holds one sentence of at most 25 words, counting a code span as one word and a link as the words of its text;
  - no documentation file contains "TODO";
  - generated-region markers exist and pair;
  - a bullet under "Unreleased changes" holds no nested list and no link to the documentation site, and the breaking changes of a subsection come before its other bullets;
  - a released changelog section holds no relative file link.
- In a repository with a documentation site, `lint-docs.cs` checks the pages against "Documentation site" above, in place of the page template and the indexes. It checks:
  - the content root is `docs/`, `docs/index.md` exists, and `docs/README.md` does not;
  - every page has a title and a summary paragraph, and no table of contents;
  - a subfolder that holds pages has a `_folder.yml` with a `title`, and no folder takes the path `preview`;
  - an anchor of a page has GitHub's form.
- The generator of the documentation site runs as a phase of `inspect.cs --gate`, when the home directory holds `docgen.json`. It reads the packages the gate built, and the gate reports each of its warnings as an error. It builds the pages under `docs/` too, and reports a link to a page or to a symbol that does not exist. `rules/project/documentation.md` says where CI runs the generator.
- A reference page that lists a code surface is pinned by a test in the test project of that code. The test fails when an item exists in the code and not on the page, or on the page and not in the code. `rules/project/documentation.md` names the pinned pages, and the code each is pinned against.
