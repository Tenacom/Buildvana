# GitLab

This file is the same in every repository that copies `.claude`. `rules/common/git-host.md` lists the operations, and this file maps each one to the commands of GitLab. The client is `glab`. `glab api` calls the REST API of the host the clone is authenticated with, and expands `:fullpath` to the URL-encoded path of the project. The project of another repository is passed URL-encoded, as `group%2Frepo`.

A request body goes in a JSON file, written with the Write tool in the scratchpad, and sent with `--input`. A single field goes with `-F`, which reads a value that starts with `@` from the file it names. `-f` sends its value as a string, `@` included.

GitLab normalizes a note before it stores it: it runs and removes quick actions, strips carriage returns, and trims trailing whitespace. A body with a line that starts with `/` loses that line.

## Identify the repository

```bash
glab api projects/:fullpath --jq '{path_with_namespace, default_branch}'
```

`path_with_namespace` is the full name.

## Read the login

```bash
glab api user --jq .username
```

## Read a PR

```bash
glab api projects/:fullpath/merge_requests/<n> \
  --jq '{iid, title, web_url, author: .author.username, description, draft, base: .target_branch, head: .source_branch, head_sha: .sha, diff_refs}'
glab api projects/:fullpath/merge_requests/<n>/diffs --paginate --jq '.[].new_path'
```

`iid` is the number. `description` is the body. `diff_refs` holds `base_sha`, `head_sha`, and `start_sha`, which "Post a review" takes.

## Find the PR of a branch

```bash
glab api "projects/:fullpath/merge_requests?source_branch=<branch>&state=opened" --jq '.[0] | {iid, title, web_url}'
```

`null` means the branch has no open PR.

## Read the checks

```bash
glab api "projects/:fullpath/repository/commits/<head commit>/statuses?ref=<head branch>" --paginate \
  --jq '.[] | {name, status, allow_failure}'
```

`status` holds a job state: `success`, `failed`, `canceled`, `skipped`, `manual`, or a state before the end of the job, such as `created`, `pending`, or `running`. Read `success` as `pass`, `failed` as `fail`, `canceled` as `cancel`, `skipped` and `manual` as `skipping`, and every other state as `pending`. A `failed` status with `allow_failure` true is `pass`. A service that posts a status, such as Codecov, appears in the same list. An empty list means the checks have not started yet.

## Wait for the checks

```bash
glab ci status --branch <head branch> --wait
```

The command returns when the pipeline ends. A status a service posts may arrive after the pipeline, so read the checks after the command returns, and wait while one is in `pending`.

## Check out the head commit

```bash
glab mr checkout <n> --branch pr-<n> --force
```

`--force` resets an existing `pr-<n>` branch when it has diverged, and refuses when the working tree has changes.

## Read the reviews

GitLab has no review object. A publish of draft notes creates one note per comment and one note for the body. The reviews of a PR are its notes that are not system notes, with `author.username` as the login.

```bash
glab api "projects/:fullpath/merge_requests/<n>/notes?sort=asc" --paginate \
  --jq '.[] | select(.system | not) | {id, author: .author.username, body}'
```

A review has no state. The round count filters on the username and the body:

```bash
glab api "projects/:fullpath/merge_requests/<n>/notes" --paginate \
  --jq '[.[] | select(.author.username == "<login>" and (.body | startswith("_Review by Claude")))] | length'
```

## Read the threads

```bash
glab api projects/:fullpath/merge_requests/<n>/discussions --paginate \
  --jq '.[] | select(.individual_note | not) | {id, resolved: (.notes[0].resolved), comments: [.notes[] | {id, author: .author.username, body, path: .position.new_path, line: .position.new_line}]}'
```

`id` is the discussion id, which "Reply in a thread" and "Resolve a thread" take.

## Read another PR

```bash
glab api projects/<group%2Frepo>/merge_requests/<other-n> --jq '{title, description}'
glab api projects/<group%2Frepo>/merge_requests/<other-n>/diffs --paginate
```

Each entry of `diffs` holds `new_path`, `old_path`, and `diff`.

## Post a review

GitLab publishes a review in one request from draft notes. Create one draft note per code-anchored comment of the review file of `skills/review-pr/SKILL.md`, then publish every draft with the body as the summary note. The publish is the one request that creates the review, so the draft notes are not a second review.

A draft note anchors with the three commits of `diff_refs` and the path of the file. `new_line` is the line of the comment in the head commit.

```bash
glab api -X POST projects/:fullpath/merge_requests/<n>/draft_notes \
  -F note=@<comment body file> \
  -f position[position_type]=text \
  -f position[base_sha]=<base_sha> -f position[head_sha]=<head_sha> -f position[start_sha]=<start_sha> \
  -f position[new_path]=<path> -f position[old_path]=<path> -F position[new_line]=<line>
glab api -X POST projects/:fullpath/merge_requests/<n>/draft_notes/bulk_publish -F note=@<body file>
```

GitLab has no verdict on a review. The verdict stays in the body, as the self-review rule of `skills/review-pr/SKILL.md` says. The URL of the review is the URL of the PR followed by `#note_<id>`, with the id of the summary note from the notes list.

## Create a PR

```bash
glab mr create --target-branch <base> --title "<title>" --description-file <file> --yes
```

The command prints the URL of the PR.

## Update the body of a PR

```bash
glab mr update <n> --description-file <file>
glab api projects/:fullpath/merge_requests/<n> --jq .description
```

## Read the closing link

```bash
glab api projects/:fullpath/merge_requests/<n>/closes_issues --jq '.[].iid'
```

## Reply in a thread

```bash
glab api -X POST projects/:fullpath/merge_requests/<n>/discussions/<discussion id>/notes -F body=@<file>
```

## Resolve a thread

```bash
glab api -X PUT projects/:fullpath/merge_requests/<n>/discussions/<discussion id> -F resolved=true
```

## Read an issue

```bash
glab api projects/:fullpath/issues/<n> --jq '{iid, id, title, description, state, labels}'
```

`iid` is the number. `id` is the id of the issue across the instance, which "Set the parent of an issue" takes.

## Create an issue

```bash
glab api -X POST projects/:fullpath/issues -f title="<title>" -F description=@<file> -f labels=<name>,<name> --jq '{iid, web_url}'
```

`labels` is a comma-separated list of label names.

## Update an issue

```bash
glab api -X PUT projects/:fullpath/issues/<n> -f title="<title>" -F description=@<file> -f labels=<name>,<name> -f state_event=close
glab api projects/:fullpath/issues/<n> --jq '{iid, title, description, state, labels}'
```

Pass the fields that change. `labels` replaces every label of the issue. `state_event` takes `close` or `reopen`.

## Read the parent and the sub-issues of an issue

GitLab has no parent/child relation between two issues. The parent of an issue is an epic, on Premium and Ultimate. The children of an issue are tasks, which are not issues. Skip the step, and say so.

## Set the parent of an issue

One mutation sets or clears the parent of any work item: an issue under an epic, or a task under an issue. `<id>` and `<parent id>` are the `id` of each work item, as "Read an issue" returns it, not the `iid`. `parentId: null` clears the parent.

```bash
glab api graphql -f query='mutation { workItemUpdate(input: { id: "gid://gitlab/WorkItem/<id>", hierarchyWidget: { parentId: "gid://gitlab/WorkItem/<parent id>" } }) { workItem { id } errors } }'
```

The mutation and its input come from the GraphQL reference and the source of GitLab, read on 2026-11-10, and not from a run against an instance. Two facts are unverified: that `glab api graphql` reaches the GraphQL endpoint, and that the work item id of an issue is its `id`. When the mutation fails, check both before anything else.

## Read the blocking issues

A block is an issue link of type `blocks` or `is_blocked_by`, on Premium and Ultimate. The list holds every link of the issue, and `link_type` says how the linked issue relates to the issue. `issue_link_id` is the id of the link, which "Set a blocking issue" takes to remove it.

```bash
glab api projects/:fullpath/issues/<n>/links --jq '.[] | {iid, project_id, link_type, issue_link_id}'
```

## Set a blocking issue

`link_type` is `is_blocked_by` when the other issue blocks the issue, and `blocks` when the issue blocks the other one. `target_project_id` is the numeric id of the project of the other issue, and `target_issue_iid` its number. Removing a link takes the `issue_link_id` from "Read the blocking issues". GitLab shows a warning when a blocked issue is closed, and closes it.

```bash
glab api -X POST projects/:fullpath/issues/<n>/links -F target_project_id=<project id> -F target_issue_iid=<other> -f link_type=is_blocked_by
glab api -X DELETE projects/:fullpath/issues/<n>/links/<issue_link_id>
```

The numeric id of the current project comes from `glab api projects/:fullpath --jq .id`.

## Labels

A label whose name holds `::` is scoped, and an issue or MR carries one label per scope: adding a second one replaces the first. Labels belong to a project or to a group, and the group labels reach every project of the group.

The first command creates a label of the project, the second one a label of a group.

```bash
glab label create --name <name> --color <hex> --description <text>
glab api -X POST groups/<group%2Fpath>/labels -f name=<name> -f color=<hex> -f description=<text>
```
