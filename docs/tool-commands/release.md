# Release

`bv release` publishes a release of the repository from a CI runner: it builds, tags, pushes the packages, and creates the release on GitHub.
This page says what the command checks, what it does step by step, and what it undoes when a step fails.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Preconditions](#preconditions)
- [The steps](#the-steps)
- [The release commit](#the-release-commit)
- [The changelog](#the-changelog)
- [The post-release commit](#the-post-release-commit)
- [Publishing](#publishing)
- [Rollback](#rollback)
- [Options](#options)
  - [The build configuration](#the-build-configuration)
  - [The version spec change](#the-version-spec-change)
- [Exit codes](#exit-codes)

---

## Preconditions

```text
bv release [OPTIONS]
```

`bv release` creates releases on GitHub, from a GitHub Actions runner.
On GitLab CI, `bv` recognizes the runner and fails when it creates the release, because the GitLab adapter creates none.
On any other machine, the command fails at once with `A release can only be created on a known cloud build platform.`

Before building anything, `bv release` checks what a build cannot change:

1. `HEAD` is on a branch, and the branch produces [public releases](../sdk-modules/versioning.md#public-releases) under `release.branches`.
2. A committer identity exists for the commits of the release.
   `bv` takes the first of three that exists.
   The three are the `git.identity` setting of `buildvana.jsonc`, the CI bot identity of the platform, and the `user.name` and `user.email` of the Git configuration.
   On GitHub Actions, the bot identity is `github-actions[bot]`.
   On GitLab CI, it is `GitLab CI`, with the address `gitlab-ci@noreply.<host>` built from [`CI_SERVER_HOST`](../environment-variables.md#ci_server_host).
3. The platform supplies push credentials.
   On GitHub Actions, they are the token that [`github.tokenEnv`](../environment-variables.md#secret-carrying-variables-named-by-the-configuration-file) names.
   Without them, `bv` warns that the push may fail, and goes on.
4. The published versions are consistent: the latest tag is not below the latest stable tag, and the current version is below neither.
5. The version spec change is computed from `--bump` and the public API files, as [`bv version advance`](version.md#bv-version-advance) computes it.
6. [`GITHUB_OUTPUT`](../environment-variables.md#github_output) is set, on GitHub Actions.

`bv` reads the latest versions from the tags reachable from `HEAD`, so the runner needs the full history.
With `actions/checkout`, that is `fetch-depth: 0`.

---

## The steps

1. Create a provisional draft release on GitHub, named `<version> [provisional]`, so that a token without permission fails the release early.
2. Rewrite `VERSION` when the version spec change is not `none`, with the tag `versioning.prereleaseTag` names.
3. For a stable version, move the unshipped public API into the shipped one.
   Every `PublicAPI.Unshipped.txt` next to a `PublicAPI.Shipped.txt` is emptied into it, and a line starting with `*REMOVED*` removes its API from the shipped file.
4. Update the changelog, as [The changelog](#the-changelog) says.
5. Create the release commit, whether or not a file changed, as [The release commit](#the-release-commit) says.
6. Check the versions once more.
   The version to publish has a Git height above 0, and is above the latest tag and the latest stable tag.
7. Check that the release tag does not exist.
8. Print `notice: Releasing version <version>.`
   From here on, the version is final.
9. Run the build pipeline from `clean` through `pack`, with the release configuration, to build, test, and produce the artifacts.
10. Finalize the new changelog section with the released version, as [The changelog](#the-changelog) says.
11. Find the produced packages: every `.nupkg` file in `artifacts\<configuration>\` whose name ends with `.<version>.nupkg`.
12. Run the [`release/post-release` hook](../hooks.md#the-releasepost-release-hook), and record the files it changed.
13. Rewrite the self-references when `release.dogfood` is `true`, as [The post-release commit](#the-post-release-commit) says.
14. Create the post-release commit, when the hook or the rewrites changed a file.
15. Push the commits.
16. Push the packages to the NuGet feed, as [Publishing](#publishing) says.
17. Collect the release assets, as [Publishing](#publishing) says.
18. Publish the release, and write the `version` step output.

Every step is undone when a later step fails, as [Rollback](#rollback) says.

---

## The release commit

`bv release` commits the files it changed before the build as `Prepare release <version> [skip ci]`.
It creates the commit even when nothing changed, because the commit settles the version.
The patch number is the [Git height](../sdk-modules/versioning.md#patch-number) of the version line, and a commit created after the build would move it.
The artifacts, the tag, and the release would then disagree by one patch.
The release tag names the release commit, whatever commits follow it.

A version whose Git height is 0 is refused, because no commit in the history carries the version line.
The usual cause is a `VERSION` file changed and not committed.
A build of the tagged commit would compute another version, so the release stops with a message naming the file to commit.

When no file changes before the build, the release commit is empty.
The major public Git hosts accept an empty commit.
A self-hosted server with a `pre-receive` hook of its own may reject it.
Two remedies exist:

- allow empty commits on the server;
- set `release.changelogUpdates` to `all`, so that every release updates the changelog, and its commit holds at least that change.
  An empty "Unreleased changes" section then fails the release, so keep at least one entry between releases, or set `release.emptyChangelog`.

When neither remedy fits, open an issue.

---

## The changelog

`CHANGELOG.md` in the home directory follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Its first section is "Unreleased changes", with subsections for each kind of change.

`release.changelogUpdates` says which releases update the changelog: `none`, `stable`, which is the default, or `all`.
A release that updates it needs content in "Unreleased changes" beyond the subsection headings.
When the section is empty, `release.emptyChangelog` supplies the text of the new section, and without the setting the release fails.
A repository without `CHANGELOG.md` skips the update, with a notice.

Before the release commit, `bv` moves the content of "Unreleased changes" under a new section, and leaves the subsection headings behind, empty.
A subsection with no content is left out of the new section.
After the build, when the version is final, `bv` gives the new section its heading, `## [<version>](<release URL>) (<date>)`.
The date is the release date, in `yyyy-MM-dd` form, with the Gregorian calendar whatever the culture of the runner.
`bv` also rewrites each relative file link of the section into a link to the file at the release tag.
The section then stays right whatever happens to the file later.
A link with a scheme, and a link to an anchor of the changelog, are left alone.

The rewrite has three limits.
A link inside a code span or a fenced block is rewritten too.
A reference-style link is not handled.
A link target is read up to the first whitespace or closing parenthesis.

---

## The post-release commit

After the artifacts are built, `bv release` runs the [`release/post-release` hook](../hooks.md#the-releasepost-release-hook), and then rewrites the self-references of the repository when `release.dogfood` is `true`.
A self-reference is a pin of a package the release produced, in one of three files:

- `global.json`, under `msbuild-sdks`;
- `dotnet-tools.json`, under `tools`;
- `Directory.Packages.props`, as a `PackageVersion` item.

`bv` splices the new version over the old one, and leaves the rest of the file as it was, line endings and comments included.
A version written as an MSBuild property, such as `$(MyVersion)`, is left alone.

The files the hook changed and the files the rewrites changed go in one commit, `Post-release updates for <version> [skip ci]`, on top of the release commit.
The commit is separate, so that the tagged commit still references the versions published before, and a build of it reproduces the released state.
The commit carries `[skip ci]`, because the packages it references are not on the feed at push time.
When neither the hook nor the rewrites changed a file, there is no post-release commit.

---

## Publishing

`bv release` pushes the commits, then every `.nupkg` file of `artifacts\<configuration>\`, with `dotnet nuget push --skip-duplicate`.
`dotnet nuget push` gets the arguments and environment variables of the `dotnet.all` and `dotnet.nugetPush` sections of `buildvana.jsonc`, and no forwarded argument.
A prerelease goes to the `nuget.feeds.prerelease` feed, and a stable version to the `nuget.feeds.release` feed.
A repository that states no prerelease feed pushes prereleases to the release feed.
The API key comes from the environment variable that the `apiKeyEnv` member of the feed names.
Without a feed, the release fails.

The release assets are collected from two sources:

- every [`*.assets.txt` file](../sdk-modules/release-asset-list.md) in the artifacts directory, where each line holds a path, a MIME type, and a description, separated by tabs;
- every `.nupkg` file, and the `.snupkg` file next to it, when one exists.

A line with the wrong shape, or one naming a file that does not exist, is a warning, and the asset is skipped.

`bv` then uploads the assets, and turns the draft into the release.
The release is named after the version, its tag names the release commit, and the prerelease flag follows the version.
Its description is a link to `CHANGELOG.md` on the release branch, followed by the notes GitHub generates.
On GitHub Actions, `bv` appends `version=<version>` to the file [`GITHUB_OUTPUT`](../environment-variables.md#github_output) names, for the later steps of the job.
The last line of a release is `notice: Published release <version> with <count> assets.`

---

## Rollback

When a step fails after the draft release exists, `bv release` undoes what it did, in reverse order:

- it resets the branch to the commit before the release commit, post-release commit included;
- when the commits were pushed, it force-pushes the previous tip;
- it deletes the release, and the tag when the release was published.

A rollback action that fails is a warning, and the rollback goes on.
Once the release is published and the step output is written, nothing is undone.

---

## Options

| Option                         | Meaning                                                                                                  |
| ------------------------------ | -------------------------------------------------------------------------------------------------------- |
| `-c`, `--configuration <NAME>` | The build configuration of the release, as [The build configuration](#the-build-configuration) says.     |
| `--bump <CHANGE>`              | The version spec change to request: `none`, `unstable`, `stable`, `minor`, or `major`.                   |
| `--check-public-api <BOOL>`    | Whether the public API files take part in the version spec change. Defaults to `release.checkPublicApi`. |
| `--dogfood <BOOL>`             | Whether the self-references are rewritten. Defaults to `release.dogfood`.                                |

`bv release` refuses a `--` separator and everything after it, because it forwards nothing.

### The build configuration

`bv release` takes the first of `--configuration`, the `release.configuration` setting, and the `dotnet.configuration` setting, and `Release` without any.
The configuration selects the artifacts directory, `artifacts\<configuration>\`, as well as the build.

### The version spec change

`--bump` requests a change to the `MAJOR.MINOR[-[tag]]` specification in `VERSION`, with the values [`bv version advance`](version.md#bv-version-advance) takes.
`bv release` runs the request through the same analysis, and never applies less than the public API requires.
`--check-public-api false` leaves the public API files out of the analysis.

---

## Exit codes

`bv release` returns the [exit codes every `bv` command returns](../tool-diagnostics.md#exit-codes).

Code 1 is a precondition that fails, a versioning anomaly, a tag that exists, an empty changelog with no substitute, or a missing feed.
Code 2 is a refusal of the command line: a `--` separator, an unknown option, or an unknown `--bump` value.
Code 3 says that a program `bv` ran failed: a `dotnet` command, `dotnet nuget push`, or the hook.
Code 130 is a run terminated with Ctrl-C.
