# Forgejo

This file is the same in every repository that copies `.claude`. `rules/common/git-host.md` lists the operations, and this file maps each one to the commands of Forgejo. The client is `curl` against the REST API, with `jq` on the responses. `buildvana.jsonc` holds both facts the commands need. The API root, `<api>` below, is the scheme and host of `repository.url` followed by `/api/v1`. The environment variable that holds the token is `hosts.forgejo.tokenEnv`, `FORGEJO_TOKEN` below and by default.

The token needs the scopes `read:user`, `write:repository`, and `write:issue`. A PR, its reviews, its comments, and the checks of a commit sit under the repository scope, and an issue and the labels of a repository under the issue scope. The labels of an organization need `write:organization` too, and the owner role in the organization. Forgejo answers 403 to a request outside the scopes of the token, with the missing scope in the message.

Every command takes the token in a header. A command that sends a body takes two more options.

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <url>
curl -sS -H "Authorization: token $FORGEJO_TOKEN" -H "Content-Type: application/json" -X POST --data-binary @<file> <url>
```

Write a request body with the Write tool, as a JSON file in the scratchpad, and send it with `--data-binary`. `-d` strips the newlines of the file.

The routes below are those of Forgejo 15, the long-term support release, read from its `v15.0/forgejo` branch on 2026-11-05. An instance without a route answers 404 to it. When a route is missing, read the version of the instance with `GET <api>/version` and tell the user.

## Identify the repository

Read the full name from the URL of the remote in `git remote -v`, as `owner/repo`. Then read the default branch.

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/repos/<owner>/<repo> | jq '{full_name, default_branch}'
```

## Read the login

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/user | jq -r .login
```

## Read a PR

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/repos/<owner>/<repo>/pulls/<n> \
  | jq '{number, title, html_url, author: .user.login, body, draft, base: .base.ref, head: .head.ref, head_sha: .head.sha}'
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/repos/<owner>/<repo>/pulls/<n>/files | jq -r '.[].filename'
```

## Find the PR of a branch

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" "<api>/repos/<owner>/<repo>/pulls?state=open" \
  | jq '[.[] | select(.head.ref == "<branch>")] | .[0] | {number, title, html_url}'
```

`null` means the branch has no open PR.

## Read the checks

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/repos/<owner>/<repo>/commits/<head commit>/status \
  | jq '.statuses[] | {name: .context, status}'
```

`status` holds `pending`, `success`, `error`, `failure`, or `warning`. Read `success` and `warning` as `pass`, `error` and `failure` as `fail`, and `pending` as `pending`. A release after 15 adds `skipped`, which is `skipping`. An empty `statuses` array means the checks have not started yet.

## Wait for the checks

Forgejo has no watch command. Read the checks, and while one is in `pending`, wait and read again.

```bash
sleep 90
```

## Check out the head commit

```bash
git fetch <remote> +refs/pull/<n>/head:pr-<n>
git switch pr-<n>
```

The `+` resets an existing `pr-<n>` branch to the head commit. `<remote>` is the remote of the repository the PR belongs to.

## Read the reviews

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/repos/<owner>/<repo>/pulls/<n>/reviews \
  | jq '.[] | {id, author: .user.login, body, state}'
```

`state` holds `APPROVED`, `REQUEST_CHANGES`, `COMMENT`, `PENDING`, or `REQUEST_REVIEW`. The round count filters on the login and the body:

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/repos/<owner>/<repo>/pulls/<n>/reviews \
  | jq '[.[] | select(.user.login == "<login>" and (.body | startswith("_Review by Claude")))] | length'
```

## Read the threads

Forgejo holds the comments per review. Read the comments of each review id from "Read the reviews". A thread is the comments that share a path and a line, across reviews. `resolver` is `null` while the thread is unresolved. "Reply in a thread" takes the review id, the path, and the line of the comment it answers.

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/repos/<owner>/<repo>/pulls/<n>/reviews/<review id>/comments \
  | jq '.[] | {id, review: .pull_request_review_id, author: .user.login, body, path, line: .position, resolved: (.resolver != null)}'
```

## Read another PR

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/repos/<other-owner>/<other-repo>/pulls/<other-n> | jq '{title, body}'
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/repos/<other-owner>/<other-repo>/pulls/<other-n>/files | jq -r '.[].filename'
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/repos/<other-owner>/<other-repo>/pulls/<other-n>.diff
```

## Post a review

Translate the review file of `skills/review-pr/SKILL.md` into the request body of Forgejo. `event` takes `APPROVED`, `REQUEST_CHANGES`, or `COMMENT`. A comment anchors with `new_position`, and has no side.

```bash
jq '{event: (if .event == "APPROVE" then "APPROVED" else .event end), body, commit_id, comments: [.comments[] | {path, body, new_position: .line}]}' <file> > <file>.forgejo.json
curl -sS -H "Authorization: token $FORGEJO_TOKEN" -H "Content-Type: application/json" -X POST --data-binary @<file>.forgejo.json <api>/repos/<owner>/<repo>/pulls/<n>/reviews
```

The response holds `id` and `html_url`.

## Create a PR

The body file holds `title`, `body`, `head`, and `base`. When the head branch is on a fork, `head` is `<fork owner>:<branch>`.

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" -H "Content-Type: application/json" -X POST --data-binary @<file> <api>/repos/<owner>/<repo>/pulls | jq '{number, html_url}'
```

## Update the body of a PR

The body file holds `body`.

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" -H "Content-Type: application/json" -X PATCH --data-binary @<file> <api>/repos/<owner>/<repo>/pulls/<n> | jq -r .body
```

The response is the updated PR, so the command reads the body back.

## Read the closing link

Forgejo has no API for the closing link. Skip the step, and say so.

## Reply in a thread

Forgejo 15 has no reply route. A reply is a comment added to the review that holds the comment it answers, on the same path and line. Forgejo accepts a comment on a submitted review of another account, and attributes it to the account that posts it. The body file holds `path`, `new_position`, and `body`, with the path and the line of the comment it answers.

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" -H "Content-Type: application/json" -X POST --data-binary @<file> <api>/repos/<owner>/<repo>/pulls/<n>/reviews/<review id>/comments
```

## Resolve a thread

Forgejo 15 has no API to resolve a thread. Skip the step, and say so. The user resolves the thread on the PR page.

## Read an issue

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/repos/<owner>/<repo>/issues/<n> | jq '{number, title, body, state, labels: [.labels[].name]}'
```

## Create an issue

The body file holds `title` and `body`. The labels go in a second request, whose file holds `labels`, a list of label names. A name that matches no label of the repository or of its organization is ignored, without an error.

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" -H "Content-Type: application/json" -X POST --data-binary @<file> <api>/repos/<owner>/<repo>/issues | jq '{number, html_url}'
curl -sS -H "Authorization: token $FORGEJO_TOKEN" -H "Content-Type: application/json" -X PUT --data-binary @<labels file> <api>/repos/<owner>/<repo>/issues/<n>/labels
```

## Update an issue

The body file holds any of `title`, `body`, and `state`, which takes `open` or `closed`. The labels file holds `labels`, the full list of label names, and the request replaces the labels of the issue with it. Send the request of each part that changes, then read the issue back.

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" -H "Content-Type: application/json" -X PATCH --data-binary @<file> <api>/repos/<owner>/<repo>/issues/<n>
curl -sS -H "Authorization: token $FORGEJO_TOKEN" -H "Content-Type: application/json" -X PUT --data-binary @<labels file> <api>/repos/<owner>/<repo>/issues/<n>/labels
curl -sS -H "Authorization: token $FORGEJO_TOKEN" <api>/repos/<owner>/<repo>/issues/<n> | jq '{number, title, body, state, labels: [.labels[].name]}'
```

## Labels

A label whose name holds `/` is scoped, and the scope is the part before the last `/`. A label marked exclusive admits no other label of its scope on the same issue or PR. Labels belong to a repository or to an organization, and the organization labels reach every repository of the organization. The instance offers label templates, the files of `options/label/` in its custom directory, at repository creation and on the labels page of a repository. A template is a YAML file with a `labels` list, and each entry has `name`, `exclusive`, `color`, and `description`.

The body file of a new label holds `name`, `color`, `description`, and `exclusive`. The first command creates a label of a repository, the second one a label of an organization.

```bash
curl -sS -H "Authorization: token $FORGEJO_TOKEN" -H "Content-Type: application/json" -X POST --data-binary @<file> <api>/repos/<owner>/<repo>/labels
curl -sS -H "Authorization: token $FORGEJO_TOKEN" -H "Content-Type: application/json" -X POST --data-binary @<file> <api>/orgs/<org>/labels
```
