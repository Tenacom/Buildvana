# GitHub

This file is the same in every repository that copies `.claude`. `rules/common/git-host.md` lists the operations, and this file maps each one to the commands of GitHub. The client is `gh`. Pass the repository as `--repo <owner>/<repo>` in every command that takes it, and never rely on a default.

## Identify the repository

```bash
gh repo view --json nameWithOwner,defaultBranchRef
```

`nameWithOwner` is the full name. `defaultBranchRef.name` is the default branch. The command resolves the repository from the remotes of the clone, and the result may differ from the repository you expect.

## Read the login

```bash
gh api user --jq .login
```

## Read a PR

```bash
gh pr view <n> --repo <owner>/<repo> \
  --json number,title,url,author,body,isDraft,baseRefName,headRefName,headRefOid,files
```

`author.login` is the author login. `isDraft` is the draft flag. `baseRefName` and `headRefName` are the branches. `headRefOid` is the head commit. `files[].path` are the changed files.

## Find the PR of a branch

```bash
gh pr view --json number,title,url,headRefName
```

Without a number, the command reads the PR of the current branch, and fails when there is none.

## Read the checks

```bash
gh pr checks <n> --repo <owner>/<repo> --json name,bucket
```

`bucket` holds `pass`, `fail`, `pending`, `skipping`, or `cancel`. When the repository reports no checks at all, the command prints `no checks reported` and exits 1. That output means the checks have not started yet.

## Wait for the checks

```bash
gh pr checks <n> --repo <owner>/<repo> --watch
```

The command returns when every check has ended. Run it in the background.

## Check out the head commit

```bash
gh pr checkout <n> --repo <owner>/<repo> --branch pr-<n> --force
```

`--force` resets an existing `pr-<n>` branch to the head commit.

## Read the reviews

```bash
gh pr view <n> --repo <owner>/<repo> --json reviews
```

`reviews[].author.login`, `reviews[].body`, and `reviews[].state` hold the fields. The round count filters on the login and the body:

```bash
gh pr view <n> --repo <owner>/<repo> --json reviews \
  --jq '[.reviews[] | select(.author.login == "<login>" and (.body | startswith("_Review by Claude")))] | length'
```

## Read the threads

```bash
gh api graphql -f query='query { repository(owner: "<owner>", name: "<repo>") { pullRequest(number: <n>) { reviewThreads(first: 100) { nodes { id isResolved comments(first: 100) { nodes { databaseId author { login } body path line } } } } } } }'
```

`id` is the thread id, which "Resolve a thread" takes. `databaseId` is the comment id, which "Reply in a thread" takes. The comments of every thread, with the replies of the author, are also one list:

```bash
gh api repos/<owner>/<repo>/pulls/<n>/comments --paginate
```

## Read another PR

```bash
gh pr view <other-n> --repo <other-owner>/<other-repo> --json title,body,files
gh pr diff <other-n> --repo <other-owner>/<other-repo>
```

## Post a review

The review file of `skills/review-pr/SKILL.md` is the request body as it stands.

```bash
gh api repos/<owner>/<repo>/pulls/<n>/reviews --method POST --input <file>
```

`event` takes `APPROVE`, `REQUEST_CHANGES`, or `COMMENT`. The response holds `id` and `html_url`.

## Create a PR

```bash
gh pr create --repo <owner>/<repo> --base <base> --title "<title>" --body-file <file>
```

The command prints the URL of the PR. The PR branch is on the fork, so `gh` asks where to push when the branch is not pushed yet. Push first.

## Update the body of a PR

```bash
gh pr edit <n> --repo <owner>/<repo> --body-file <file>
gh pr view <n> --repo <owner>/<repo> --json body
```

## Read the closing link

```bash
gh api graphql -f query='query { repository(owner: "<owner>", name: "<repo>") { pullRequest(number: <n>) { closingIssuesReferences(first: 10) { nodes { number } } } } }'
```

GitHub derives the link only when the base branch is the default branch.

## Reply in a thread

```bash
gh api --method POST repos/<owner>/<repo>/pulls/<n>/comments/<comment id>/replies -F body=@<file>
```

## Resolve a thread

```bash
gh api graphql -f query='mutation { resolveReviewThread(input: { threadId: "<thread id>" }) { thread { isResolved } } }'
```

## Read an issue

```bash
gh issue view <n> --repo <owner>/<repo> --json number,title,body,state,labels
```
