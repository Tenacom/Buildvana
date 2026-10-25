# Release

`bv release` publishes a release of the repository: it builds, tags, pushes the packages, and creates the release on the Git host.
This page says what the command checks, what it does step by step, and what it undoes when a step fails.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Preconditions](#preconditions)
- [The release remote](#the-release-remote)
- [The steps](#the-steps)
- [The release commit](#the-release-commit)
- [The release tag](#the-release-tag)
- [The changelog](#the-changelog)
- [The post-release commit](#the-post-release-commit)
- [Publishing](#publishing)
  - [Publishing on GitHub](#publishing-on-github)
  - [Publishing on GitLab](#publishing-on-gitlab)
  - [Publishing on an unknown host](#publishing-on-an-unknown-host)
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

`bv release` runs on every [CI platform](../introduction.md#ci-platforms), Local included.
A release from a developer machine goes through the same steps as a release from a CI runner.
The Git host decides where the release is published: GitHub, GitLab, or a host `bv` does not know.

Before building anything, `bv release` checks what a build cannot change:

1. `buildvana.jsonc` states [`repository.url`](../configuration-file.md#settings), the URL of the repository the releases go to.
2. The clone has a remote that names that repository, as [The release remote](#the-release-remote) says.
3. `bv` knows the Git host of `repository.url`, or `release.allowUnknownHost` is `true`.
   `bv` takes `repository.host` when it is not `auto`.
   Otherwise, `github.com` and any host whose name ends in `.ghe.com` are GitHub, and `gitlab.com` is GitLab.
   Otherwise, a host whose name is the host name of [`GITHUB_SERVER_URL`](../environment-variables.md#github_server_url) is a GitHub Enterprise Server, on GitHub Actions.
   Otherwise, a host whose name is the host name of [`CI_SERVER_URL`](../environment-variables.md#ci_server_url) is a self-managed GitLab instance, on GitLab CI.
   Any other host is unknown.
4. `HEAD` is on a branch, and the branch produces [public releases](../sdk-modules/versioning.md#public-releases) under `release.branches`.
5. A committer identity exists for the commits of the release.
   `bv` takes the first of three that exists.
   The three are the `git.identity` setting of `buildvana.jsonc`, the bot identity of the CI platform, and the `user.name` and `user.email` of the Git configuration.
   On GitHub Actions, the bot identity is `github-actions[bot]`.
   On GitLab CI, it is `GitLab CI`, with the address `gitlab-ci@noreply.<host>` built from [`CI_SERVER_HOST`](../environment-variables.md#ci_server_host).
   `bv` commits with that identity, and writes nothing to the Git configuration.
6. The host supplies Git credentials.
   On GitHub, they are `x-access-token` and the token that [`hosts.github.tokenEnv`](../environment-variables.md#secret-carrying-variables-named-by-the-configuration-file) names.
   On GitLab, they are a username and the token that `hosts.gitlab.tokenEnv` names, as [Publishing on GitLab](#publishing-on-gitlab) says.
   An unknown host supplies none.
   Without them, `bv` warns that the fetch and the push may fail, and goes on.
7. `bv` fetches the tags of the release remote.
8. The published versions are consistent: the latest tag is not below the latest stable tag, and the current version is below neither.
9. The version spec change is computed from `--bump` and the public API files, as [`bv version advance`](version.md#bv-version-advance) computes it.
10. [`GITHUB_OUTPUT`](../environment-variables.md#github_output) is set, on GitHub Actions.

[`bv info`](info.md) reports the release remote, the Git host, the token, and the committer identity, without releasing.

`bv` reads the latest versions from the tags reachable from `HEAD`, so the clone needs the full history.
With `actions/checkout`, that is `fetch-depth: 0`.

---

## The release remote

The release remote is the remote of the clone that `bv release` fetches the tags from, and pushes the commits and the tag to.
It is the remote whose URL names the repository that `repository.url` names.
In CI, it is usually `origin`.
In a clone of a fork, it is the remote that names the upstream repository, whatever its name.

Two URLs name the same repository when their host names are equal and their paths are equal, both without regard to case.
The scheme, the port, the user, and a `.git` suffix play no part.
An SSH URL and an HTTPS URL of a repository therefore match.

`bv` pushes over HTTP and HTTPS alone.
When the only remote that matches has another URL, such as an SSH one, the release fails, and the message names the remote.
Add a remote with the HTTPS URL of the repository.
When no remote matches, the release fails, and the message names the remotes of the clone.

---

## The steps

1. Check that the token can publish on the host, so that a token without permission fails the release early.
   On GitHub, create a provisional draft release, named `<version> [provisional]`.
   On GitLab, read the releases of the project, and check the role of the token, as [Publishing on GitLab](#publishing-on-gitlab) says.
2. Rewrite `VERSION` when the version spec change is not `none`, with the tag `versioning.prereleaseTag` names.
3. For a stable version, move the unshipped public and internal API into the shipped one.
   Every `PublicAPI.Unshipped.txt` next to a `PublicAPI.Shipped.txt` is emptied into it, and a line starting with `*REMOVED*` removes its API from the shipped file.
   Every `InternalAPI.Unshipped.txt` next to an `InternalAPI.Shipped.txt` is emptied into it the same way.
4. Update the changelog, as [The changelog](#the-changelog) says.
5. Create the release commit, whether or not a file changed, as [The release commit](#the-release-commit) says.
6. Check the versions once more.
   The version to publish has a Git height above 0, and is above the latest tag and the latest stable tag.
7. Check that the release tag does not exist.
8. Print `notice: Releasing version <version>.`
   From here on, the version is final.
9. Run the build pipeline from `clean` through `pack`, with the release configuration, to build, test, and produce the artifacts.
   The run passes `ContinuousIntegrationBuild=true` to `dotnet` on every platform, Local included, because release artifacts are deterministic.
10. Finalize the new changelog section with the released version, as [The changelog](#the-changelog) says.
11. Find the produced packages: every `.nupkg` file in `artifacts\<configuration>\` whose name ends with `.<version>.nupkg`.
12. Run the [`release/post-release` hook](../hooks.md#the-releasepost-release-hook), and record the files it changed.
13. Rewrite the self-references when `release.dogfood` is `true`, as [The post-release commit](#the-post-release-commit) says.
14. Create the post-release commit, when the hook or the rewrites changed a file.
15. Collect the release assets, and have the host check them, as [Publishing](#publishing) says.
16. Create the release tag, push the commits, and push the tag, as [The release tag](#the-release-tag) says.
17. Push the packages to the NuGet feed, as [Publishing](#publishing) says.
18. Publish the release on the host, and write the `version` step output.

Every step is undone when a later step fails, as [Rollback](#rollback) says.

---

## The release commit

`bv release` commits the files it changed before the build as `Prepare release <version> [skip ci]`.
It creates the commit even when nothing changed, because the commit settles the version.
The patch number is the [Git height](../sdk-modules/versioning.md#patch-number) of the version line, and a commit created after the build would move it.
The artifacts, the tag, and the release would then disagree by one patch.

A version whose Git height is 0 is refused, because no commit in the history carries the version line.
The usual cause is a `VERSION` file changed and not committed.
A build of the tagged commit would compute another version, so the release stops with a message naming the file to commit.

When no file changes before the build, the release commit is empty.
The major public Git hosts accept an empty commit.
A self-hosted Git host with a `pre-receive` hook of its own may reject it.
Two remedies exist:

- allow empty commits on the Git host;
- set `release.changelogUpdates` to `all`, so that every release updates the changelog, and its commit holds at least that change.
  An empty "Unreleased changes" section then fails the release, so keep at least one entry between releases, or set `release.emptyChangelog`.

When neither remedy fits, open an issue.

---

## The release tag

`bv release` creates the release tag itself, as a lightweight tag named after the version.
The tag names the release commit, whatever commits follow it.
`bv` creates it after the build, because finalizing the changelog amends the release commit.

`bv` pushes the branch to the release remote, then the tag.
A tag of the same name on the remote fails the release, unless it names the same commit.
`bv` never moves a tag of the remote.
When the remote rejects a push, the release fails.
So does a push of the branch when the branch of the remote has commits that the local branch lacks.

The host publishes the release on the tag that is already there.
A workflow that the push of a tag triggers therefore runs before the release page exists.

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

After the commits and the tag, `bv release` pushes every `.nupkg` file of `artifacts\<configuration>\`, with `dotnet nuget push --skip-duplicate`.
`dotnet nuget push` gets the arguments and environment variables of the `dotnet.all` and `dotnet.nugetPush` sections of `buildvana.jsonc`, and no forwarded argument.
A prerelease goes to the `nuget.feeds.prerelease` feed, and a stable version to the `nuget.feeds.release` feed.
A repository that states no prerelease feed pushes prereleases to the release feed.
The API key comes from the environment variable that the `apiKeyEnv` member of the feed names.
Without a feed, the release fails.

The release assets are collected from two sources:

- every [`*.assets.txt` file](../sdk-modules/release-asset-list.md) in the artifacts directory, where each line holds a path, a MIME type, and a description, separated by tabs;
- every `.nupkg` file, and the `.snupkg` file next to it, when one exists.

A line with the wrong shape, or one naming a file that does not exist, is a warning, and the asset is skipped.
`bv` collects the assets before it pushes the commits, so that an asset the host refuses fails the release before anything leaves the clone.

`bv` then publishes the release on the Git host, as the sections below say.
On GitHub Actions, `bv` appends `version=<version>` to the file [`GITHUB_OUTPUT`](../environment-variables.md#github_output) names, for the later steps of the job.
The last line of a release is `notice: Published release <version> with <count> assets.`

### Publishing on GitHub

`bv` reaches the API of the host of `repository.url`.
For `github.com`, the API is at `https://api.github.com`.
A host whose name ends in `.ghe.com` belongs to GitHub Enterprise Cloud with data residency, and its API is at `https://api.<host>`.
`bv` takes any other host for a GitHub Enterprise Server, whose API is at `https://<host>/api/v3`.
An asset upload goes to the address that GitHub states in the answer to the creation of the draft release.

`bv` then uploads the assets, and turns the draft into the release.
The release is named after the version, its tag is the release tag, and the prerelease flag follows the version.
Its description is a link to `CHANGELOG.md` on the release branch, followed by the notes GitHub generates.

GitHub names each asset after its file, so two assets of a release differ in file name.
Two assets with the same file name fail the release before the push of the commits.

### Publishing on GitLab

`bv` reaches the API of the host of `repository.url` at `https://<host>/api/v4`, on `gitlab.com` and on a self-managed instance alike.
`bv` recognizes a self-managed instance on its own GitLab CI runners.
On any other CI platform, set `repository.host` to `gitlab`.

The token that `hosts.gitlab.tokenEnv` names is one of two kinds:

- a personal, project, or group access token, which pushes with the username `oauth2`;
- the job token of GitLab CI, when `hosts.gitlab.tokenEnv` is `CI_JOB_TOKEN`, which pushes with the username `gitlab-ci-token`.

GitLab exposes the job token through `CI_JOB_TOKEN` alone, so the name of the variable decides the kind.
The comparison is case-sensitive.
A job token copied into a variable of another name counts as an access token, and GitLab refuses it.

The token needs the Maintainer role on the project, because a rollback deletes the generic package of the release.
With an access token, `bv` reads the role of the user of the token before the release commit.
A role below Maintainer fails the release.
A user who is not a member of the project passes the check, such as an administrator of the instance.
Such a user either can delete the package, or lacks push access and fails at the push of the release commit.
GitLab states no role for a job token, so `bv` checks none.

A job token has the access of the user who started the job.
It pushes only when the project enables "Allow Git push requests to the repository".
The setting is under **Settings > CI/CD > Job token permissions**, and is generally available from GitLab 18.4.

`bv` uploads each asset to the generic package registry of the project, as one file of one package.
The package is named after the repository, the last segment of the path of `repository.url`.
Its version is the release tag.
`bv` then creates the release, named after the version, on the release tag.
Each asset gets a release link of type `package`, which points at the package file.
The name of the link is the description of the asset, and its direct asset path is `/<file name>`.

GitLab has no prerelease flag, so a prerelease looks like any other release.
GitLab generates no release notes, so the description of the release is a link to `CHANGELOG.md` on the release branch.

GitLab accepts an asset only when its file name meets the rules of the generic package registry and of release links:

- the file name holds only ASCII letters, digits, `.`, `_`, and `-`, and its first character may also be `+`;
- the last character is a letter or a digit;
- the file name has 3 to 127 characters.

Two assets of a release also differ in file name and in description.
An asset that breaks a rule fails the release before the push of the commits.

### Publishing on an unknown host

On a host `bv` does not know, there is no release page, and `bv` says so with a notice.
The tag, the commits, and the packages are pushed all the same.
The new changelog section gets a heading without a link, and its relative file links stay as they are, each with a notice.

---

## Rollback

When a step fails, `bv release` undoes what the earlier steps did, in reverse order:

- it deletes the release tag from the release remote, when it was pushed, and from the clone;
- it resets the branch to the commit before the release commit, post-release commit included;
- when the commits were pushed, it force-pushes the previous tip;
- on GitHub, it deletes the release, draft or published;
- on GitLab, it deletes the release, then the generic package, each when it exists.

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
| `--allow-unknown-host <BOOL>`  | Whether a release proceeds on an unknown Git host. Defaults to `release.allowUnknownHost`.               |

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
A fetch or a push that fails returns code 1 as well.
Code 2 is a refusal of the command line: a `--` separator, an unknown option, or an unknown `--bump` value.
Code 3 says that a program `bv` ran failed: a `dotnet` command, `dotnet nuget push`, or the hook.
Code 130 is a run terminated with Ctrl-C.
