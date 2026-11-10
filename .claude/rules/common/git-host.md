# Git host

This file is the same in every repository that copies `.claude`. A fact specific to one repository lives in another rule file.

"The Git host" is the server hosting a repository remote. Pushes and CI happen on `origin`. Issue and PR references usually point to the upstream repository, which in a fork may be on a different host. Determine the platform separately for each remote you act on.

## Platforms

The platforms are GitHub, Forgejo, and GitLab. They differ in APIs, CLI tooling, CI configuration, reference syntax, and Markdown rendering. Before using any of these, identify the platform and its version from the host information available to you: project instructions first, then user-level instructions. `rules/project/repository.md` names the hosts of the repository and their platforms.

Do not infer the platform from the remote URL alone, because a self-hosted domain says nothing about the software. Do not infer it from repository contents such as `.github/` or `.gitlab-ci.yml`, because a migrated repository keeps the files of the old platform. If a host is not covered by any instructions, ask before doing anything platform-specific. The only hosts you may assume without being told are `github.com` (GitHub), `codeberg.org` (Forgejo), and `gitlab.com` (GitLab). Each hosts the latest stable version of its platform.

To check a platform version when a feature requires it:

- Forgejo (including `codeberg.org`): `GET /api/v1/version`, usually without authentication. The result looks like `15.0.2+gitea-1.22.0`, and only the part before `+` is the Forgejo version. A `-dev` version, such as `16.0.0-dev-798-…`, is a pre-release snapshot. Treat it as the previous major version when checking a minimum version requirement.
- GitLab (self-managed): `glab api --hostname <host> version`, which requires authentication.
- GitHub Enterprise Server: `GET /api/v3/meta`. The `installed_version` field holds the version.

## Host files

The rule files and the skills name an operation on the host, such as "read the PR" or "resolve the thread", and never a command. `.claude/hosts/<platform>.md` is the host file of a platform. It maps every operation of "Operations" below to the commands of its platform, and names the fields of each result. The three host files are `github.md`, `forgejo.md`, and `gitlab.md`. `rules/project/repository.md` names the host file of the repository.

Read the host file in full before the first operation of a session, and take every command from it. A host file sits outside `rules/`, so that a session loads the one it needs and not three.

Use the client the host file names, and never an MCP server of the host. The classifier denies the writes of an MCP server, and the client keeps one login.

## Vocabulary

The rule files use one name per thing on every platform. The name is GitHub's where the platforms differ.

| Name          | GitHub              | Forgejo                       | GitLab                                   |
| ------------- | ------------------- | ----------------------------- | ---------------------------------------- |
| PR            | pull request        | pull request                  | merge request                            |
| thread        | review thread       | review conversation           | discussion                               |
| check         | check               | commit status                 | commit status, which a job or a service posts |
| review        | review              | review                        | the notes one publish of draft notes creates |
| login         | login               | login                         | username                                 |
| head commit   | `headRefOid`        | `head.sha`                    | `sha`                                    |
| full name     | `nameWithOwner`     | `full_name`                   | `path_with_namespace`                    |
| sub-issue     | sub-issue           | none                          | child item                               |
| blocked by    | blocked by          | dependency                    | issue link of type `is_blocked_by`       |

A reference such as `#1` and the `Closes #N` phrase work on every platform. `documentation.md` says where each platform turns a reference into a link.

## Labels

A label scope groups labels by a prefix of their name, such as `area` in `area/docs`. The platforms differ on the separator, and on whether a scope admits one label at a time. The host file of each platform says how its labels work and how a label is created. `rules/project/repository.md` names the scopes of the repository, and says which labels a workflow applies and which are applied by hand.

## Operations

Each host file has one section per operation below, with the same heading. An operation reads or writes what the list says, and the rule files rely on nothing else. When a platform has no command and no API for an operation, its host file says so. Skip the step that uses the operation, and say so.

An operation that no section covers is still done with the client the host file names, from the documentation of the platform. Say that the command is not from the host file, and show it before running it. When the operation will recur, propose a section for it in this file and in the three host files.

- **Identify the repository.** From the clone: the full name of the repository, `owner/repo`, and its default branch. The remote the repository lives on is the one whose URL names the full name.
- **Read the login.** The login of the account the client is configured with.
- **Read a PR.** Number, title, URL, author login, body, draft flag, base branch, head branch, head commit, and the paths of the changed files.
- **Find the PR of a branch.** The number of the open PR whose head branch is the one given.
- **Read the checks.** For the head commit: the name of each check and its state, as one of `pass`, `fail`, `pending`, `skipping`, and `cancel`. The host file maps the states of its platform onto these five.
- **Wait for the checks.** Return when no check of the head commit is in `pending`.
- **Check out the head commit.** Put the head commit of a PR in the working tree, on a branch named `pr-<n>`, and reset the branch when it exists.
- **Read the reviews.** For each review of a PR: author login, body, and state. The round count of `skills/review-pr/SKILL.md` reads the body.
- **Read the threads.** For each thread of a PR: its id, whether it is resolved, and each comment with its id, author login, body, path, and line.
- **Read another PR.** Title, body, changed files, and diff of a PR in another repository of the same host.
- **Post a review.** From the review file that `skills/review-pr/SKILL.md` describes: the verdict, the body, the head commit, and the code-anchored comments, in one publish. Two publishes make two reviews, and two reviews count as two rounds.
- **Create a PR.** From a title and a body file, against a base branch. The number and the URL of the created PR.
- **Update the body of a PR.** From a body file. Then read the body back.
- **Read the closing link.** The issues the host will close when it merges the PR. The host derives the link from the body, so a stored body does not prove the link.
- **Reply in a thread.** Post a body file as a reply to a comment of the thread.
- **Resolve a thread.** Mark the thread resolved.
- **Read an issue.** Title, body, state, and labels.
- **Create an issue.** From a title, a body file, and label names. The number and the URL of the created issue. The host applies an issue template in its web UI alone, so the body file carries the headings of the template, one `### <field label>` per field, in its order.
- **Update an issue.** Any of: the title, the body from a body file, the full list of label names, and the state, `open` or `closed`. Then read the issue back.
- **Read the parent and the sub-issues of an issue.** The number of the parent, when the issue has one, and the numbers of its sub-issues.
- **Set the parent of an issue.** Make an issue a sub-issue of another, or clear its parent. A platform without a parent/child relation between issues has no mapping, and its host file names the nearest relation it has.
- **Read the blocking issues.** The numbers of the issues that block the issue, and of the issues it blocks.
- **Set a blocking issue.** Add or remove "blocked by" between the issue and another, in either direction. The operation states the relation and not its effect, because the platforms differ on what a block does when the blocked issue is closed. Each host file says what its platform does.

## What the platforms refuse

- GitHub refuses a verdict from the author of the PR, and a code-anchored comment on a line outside the diff.
- Forgejo refuses neither. It refuses a request for changes with an empty body.
- GitLab refuses an approval from the author of the PR by default, and has no verdict on a review. It anchors a comment with the three commits of the diff, and accepts an unchanged line.

A self-review therefore carries no verdict of the host on any platform, and states its verdict in the body, as `skills/review-pr/SKILL.md` says. One rule then holds everywhere.
