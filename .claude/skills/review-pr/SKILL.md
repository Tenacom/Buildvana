---
name: review-pr
description: Review a pull request in this repository and post the review.
argument-hint: "[pr-number]"
disable-model-invocation: true
allowed-tools: Bash(gh *) Bash(glab *) Bash(curl *) Bash(jq *) Bash(git *) Bash(sleep *)
---

# Review rules

This file is the same in every repository that copies `.claude`.

These rules govern a review of a pull request in this repository. Every command on the Git
host comes from the host file that `rules/project/repository.md` names, as
`rules/common/git-host.md` says. Where a rule names an operation in quotes, such as "Read a
PR", the host file has a section of that name with the command. Read the host file in full
before the first command on the host.

## Rounds

- A _round_ is a review you post, together with the author's reactions to it. The
  reactions are replies in threads, further comments, and additional commits.
- A review posted by another account is not a round, even when its body starts with the
  marker line. Another user can run this skill on the same PR. A PR reviewed twice by
  another user with this skill and once by you is at your second round.
- Every review you post starts with a marker line, defined in "Posting the review".
  Count the reviews that your account posted and whose body starts with the marker line.
  The current round is that count plus one.
- Take `<login>` from "Read the login". The round count is the command of "Read the reviews"
  that filters on the login and the marker line. See "Preliminary actions".
- The author addresses a finding in one of two ways. The author adds a commit that
  solves the finding, or rejects the finding with a rationale.
- Read each fix in the tree at the head commit. Do not verify a fix against the author's
  description of it, and do not run anything to verify it.
- Do not report again a finding the author declined with a rationale, whatever account
  reported the finding. Report it again only when the rationale rests on a wrong fact,
  and name the wrong fact.
- From the second round on, expect zero findings.
- Do not report what you checked and found correct.
- When a fix introduces a new defect, report the new defect. Do not look for a finding
  in order to justify a round.

## Preliminary actions

Do all of the following before you read any code. When a step cannot be completed, stop
and tell the user why.

- Read the repository name, the branch name, the PR number, and the head commit from
  commands you run during this review. A wrong value puts the review on the wrong PR.
- Do not take any of these four values from your memory of a previous session.
- A resumed transcript, a summary of a compacted transcript, and a file in the repository
  are not sources of these four values either.
- When you cannot point at the command output that carries one of these four values, run
  the command again.
- When the user names a PR or a branch, verify the name against the repository before you
  use it.
- Read the clone you are in, and the branch you are on.

  ```bash
  git rev-parse --show-toplevel
  git branch --show-current
  ```

- Read the full name and the default branch of the repository with "Identify the
  repository". The repository the client resolves may differ from the repository you
  expect.
- Record the branch that `git branch --show-current` prints. You return to that branch
  after you post the review.
- When the command prints nothing, stop and tell the user. A review needs a branch to
  return to.
- When the branch is named `pr-` followed by a number, an earlier review left it behind.
  Switch to the default branch, and record that branch instead.
- `$ARGUMENTS` holds the PR number when the user passed one. When `$ARGUMENTS` is empty,
  read the PR of the current branch with "Find the PR of a branch".
- Do not guess a number. When the command finds no PR, stop and tell the user.
- Read the PR metadata with "Read a PR". Pass the number and the repository in this command
  and in every later command on the host. Do not rely on a default.
- Report the number, the title, and the URL to the user before you review. Take all three
  from the output of the command.
- Read the PR text. The PR text states what the PR sets out to do. Measure every finding
  against the PR text.
- When the PR is a draft, say so and ask the user before you go on.
- When the PR text names another PR to co-deploy with it, read that PR too. See "Pull
  requests deployed together".
- Record the head commit. Review the tree at the head commit.
- Read the PR again before you post. When the head commit has changed, verify the review
  again against the new head commit.
- Run `git status --porcelain` again before you post. When the command prints a line,
  the working tree has changed since the review started. Stop and tell the user, because
  the files you read may differ from the head commit.
- Compare the PR author with the login of "Read the login".
- When the two accounts match, the review is a _self-review_.
- Count the round. See "Rounds".
- Read the state of the PR checks with "Read the checks". The state of a check is `pass`,
  `fail`, `pending`, `skipping`, or `cancel`. Read `cancel` as `fail`, and `skipping` as
  `pass`.
- While a check is in `pending`, wait and read the state again.

  ```bash
  sleep 90
  ```

- Give up after twenty waits. Tell the user the checks are still running, and do not
  review.
- `rules/project/pr-checks.md` has one section per check whose failure does not stop a
  review by itself. The section says what to read, and which of three outcomes to pick:
  ignore the failure, report a finding, or stop.
- When a check is in `fail` and the file has no section for it, stop and tell the user
  which check failed. Do not review a PR whose checks fail. The author fixes the failure
  and pushes a new head commit, and the review starts again from the beginning.
- When the file has a section for the check, follow the section. A finding the section
  produces follows "What to report" like any other finding, and comes after the
  correctness findings.
- Read about a failure what its section names, and nothing else. Do not read the logs of
  a check, and do not diagnose a failure the file does not cover. The failure belongs to
  the author. See "What the gate and CI already cover".
- The host file says what the command prints when the repository reports no checks at
  all. That output is not a failure. This repository has CI, so that output means the
  checks have not started yet. Wait and read the state again.
- The checks belong to the commit they ran on. When you read the PR again before you post
  and the head commit has changed, wait for the checks of the new head commit too.
- When the working tree is dirty, stop and tell the user. Do not stash the changes. Do
  not review from the diff alone. The working tree is dirty when this command prints a
  line.

  ```bash
  git status --porcelain
  ```

- Read the commit the working tree is on.

  ```bash
  git rev-parse HEAD
  ```

- When the output equals the head commit, the working tree already holds it. Stay on the
  current branch, and check out nothing. The clone of the PR author is in this state
  after a push.
- Otherwise, put the head commit in the working tree with "Check out the head commit", on
  a branch named after the PR number. PR 345 gets the branch `pr-345`.
- The checkout resets an existing `pr-<n>` branch to the head commit. Never commit to a
  `pr-<n>` branch. The reset then discards nothing.
- After a checkout, check the output of `git rev-parse HEAD` against the head commit.
  When the two differ, stop and tell the user. The check ties the working tree to the PR
  you read.
- Find the remote of the repository the PR belongs to. That remote is the one whose URL
  points to the full name of the repository. Remote names vary between clones, so
  identify the remote by its URL.

  ```bash
  git remote -v
  ```

- Update the base branch from that remote. Then read the diff that defines the PR.

  ```bash
  git fetch <remote> <base>
  git diff --stat <remote>/<base>...HEAD
  ```

- The three dots select the diff from the merge base. Do not diff against the tip of the
  base branch.
- Read every previous review in full, whatever account posted it. Read the review bodies,
  the code-anchored comments, and the author's replies, with "Read the reviews" and "Read
  the threads".
- Read what the repository states about itself. Read `CLAUDE.md`, the files under
  `.claude/rules/`, `README.md`, and the files they point to. Read them before you judge
  the code against them.
- Read each changed file whole. A hunk does not show enough to judge an outcome.

## What the gate and CI already cover

- `dotnet run .claude/tools/inspect.cs --gate` runs before every push to the PR branch.
  It checks the documentation, builds the code, runs the tests, and analyzes the solution
  with ReSharper at WARNING severity and above. The CI that `rules/project/repository.md`
  names runs the build and the tests again at every push.
- The checks are the result of that CI on the head commit. A passing check is a fact
  about the code. Do not confirm it.
- Do not build the code. Do not run the tests. Do not run a linter, a formatter, or a
  static analysis tool. Do not run the code.
- A review runs `git`, `sleep`, and the commands of the host file. No other command
  belongs in a review, with one exception. A section of `rules/project/pr-checks.md` may
  name a read-only request to a service it names, with `curl`, and may name a filter on
  the response. It never names anything that runs the code or the toolchain.
- The review adds what the gate cannot produce: a judgement about the outcome the code
  produces and about the contracts it holds. Reading gives you that judgement. Running
  the toolchain does not.
- Judge a test by reading it. A test that pins nothing and a test that pins the wrong
  thing both pass. That is why you report them and the gate does not.
- _Verify_ means read. Verify a fix, and verify the code against a contract, by reading
  the tree at the head commit.
- Do not report formatting, line lengths, redundant casts, unused symbols, naming
  diagnostics, or anything else the gate reports.
- Do not measure by hand what a tool measures. A hand count of characters or a hand scan
  of files is unreliable, and it duplicates a check the author has already run.
- When you expect the build or a test to fail, you have misread the code, or the test is
  weak. Do not run the toolchain to find out which. Read the code again, and report a
  finding only when you can name the input, the path through the code, and the wrong
  outcome.

## Sources

- A review reads three sources: what the host holds about the PR, the files that git
  tracks at the head commit, and the NuGet packages the solution restores, under the
  package cache that `dotnet nuget locals global-packages --list` prints.
- What the host holds about the PR includes a PR deployed together with it. See "Pull
  requests deployed together".
- A section of `rules/project/pr-checks.md` may name a fourth source for the failed
  check it covers, such as the service that posts the check.
- To list the files of the repository, run `git ls-files`. The command prints the
  tracked files, and a PR can change no other file.

## Scope

- The PR is the diff from the merge base to the head commit.
- Report a defect the PR introduces. Report a defect the PR makes reachable.
- A defect outside the PR's changes belongs to another PR. Do not report it.
- Report a defect outside the PR's changes only when it stops the PR's changes from
  working correctly. Name the failure it causes.
- Do not widen the scope from one round to the next.

## Pull requests deployed together

- The PR text may name another PR to co-deploy with it, in another repository.
- Read the other PR and the changes it makes, with "Read another PR".
- Check that the two sets of changes work together. Check that the caller uses each
  interface with the names, the arguments, and the result shape the callee defines.
- When the two sides disagree, name the contract each side assumes. Decide which side is
  wrong. Do not assume that the PR you review holds the mistake.
- Report the mismatch only when the PR you review holds the wrong side. See "Scope".
- When the other PR holds the wrong side, do not report the mismatch as a finding. Write
  one sentence at the end of the review body, and name the other PR and the mismatch.
- That sentence is not a finding. It does not block the merge, and the author owes no
  answer to it.
- Do not add detail to that sentence. The other PR gets a review under these same rules.

## What to report

- Report a finding only when it blocks the merge. Everything else costs the author
  another round, and rounds are the expensive part of a review.
- Report correctness problems first.
- A finding blocks the merge when it names one of these:
  - an outcome the code gets wrong
  - an outcome the code gets right by accident
  - a mistake in the public API
  - a contract stated in this repository that the code breaks
  - a test that pins nothing
  - a test that pins the wrong thing
  - a risk of data loss
  - a risk of a broken release
- A finding must name what goes wrong. When you cannot name a case where the code does
  the wrong thing, and cannot name a contract the code breaks, do not report the finding.
- In the first round only, report a misleading name, a wrong comment, and an error in
  documentation, in a changelog bullet, or in the PR text. Put such findings in their
  own section, marked as non-blocking. Do not report a finding of this kind after the
  first round.
- Never report a commit message, whatever rule it breaks. The wording, the verb form,
  and the orthography of a commit message are outside the scope of a review.
- Never report a branch name, whatever rule it breaks.
- The reason is the cost of the fix. A commit message and a branch name are already
  pushed when you read them. A fix rewrites the history of the branch, or needs a new
  PR.
- The author must address every finding you report. A finding about a name or a comment
  is addressed like any other. See "Accepting a PR".
- When you find nothing, say so plainly. "No blocking findings" is a complete review. Do
  not pad it.
- Use a code-anchored comment for a finding only. Do not use a code-anchored comment to
  acknowledge a fix.

## Several occurrences of one defect

- Search the PR's changes for every occurrence of a defect before you report the defect.
- Write one finding for the defect. Name every occurrence of the defect inside the PR's
  changes.
- Do not split one defect into one finding per line.
- The author fixes what you name. A finding that names one occurrence out of three
  brings the defect back in the next round.
- Do not name an occurrence outside the PR's changes. See "Scope".

## Language

- Write the review in the language of the PR text. In this repository that language is
  English.
- Write the marker line in English, whatever the language of the review. The round count
  matches the marker line exactly. See "Rounds".

## Accepting a PR

- In the first round, accept the PR only when you have no findings.
- From the second round on, accept the PR when the author has addressed every previous
  finding and has introduced no new problem.
- Leave no pending work when you accept the PR. One remaining finding of any kind is
  enough to request changes.
- Do not add a code-anchored comment when you accept the PR.

## Posting the review

- Post the review as soon as you have written it. Do not ask the user to approve it
  first.
- Start the body of the review with the marker line and a horizontal rule.

  ```markdown
  _Review by Claude._

  ---
  ```

- Post the body and every code-anchored comment in one publish, with "Post a review". Two
  publishes produce two reviews, and two reviews count as two rounds.
- Take the repository and the number from the metadata you read. Check them against the
  URL of the PR before you send the request.
- `<file>` is the review file that `session.md` names, under "Scratchpad":
  `.claude/scratchpad/reviews/YYYY-MM-DD_HH.mm.ss_<n>.json`. Take the timestamp when the
  review starts.
- The review file holds four fields. `event` holds the verdict. `body` holds the
  review text. `commit_id` holds the head commit you recorded. `comments` holds an array
  of objects with the fields `path`, `line`, `side`, and `body`. The file has this shape
  on every platform, and the host file says how it becomes the request of its platform.
- Set `event` to `APPROVE` when you accept the PR.
- Set `event` to `REQUEST_CHANGES` when you report at least one finding.
- Take each `line` from the head commit. Anchor each code-anchored comment to a line the
  PR changes. A comment outside the diff names nothing the PR did, and a platform may
  reject it.
- When a defect has several occurrences, write one comment. Anchor the comment at one
  occurrence, and name the other occurrences in the body of the comment.

## Self-reviews

- Set `event` to `COMMENT` for a self-review, on every platform. GitHub rejects a verdict
  from the author of the PR, and one rule holds everywhere.
- A `COMMENT` review carries no verdict. State the verdict in the body, under the marker
  line.
- Start the body of a self-review with these lines, then go on like any other review.

  ```markdown
  _Review by Claude. Self-review._

  **👍 Approved.**

  ---
  ```

- Write `**⚠️ Changes requested.**` instead of `**👍 Approved.**` when you report at
  least one finding.

## After posting

- When you stayed on the current branch, there is no review branch to leave. Skip this
  section.
- When you checked out a `pr-<n>` branch, leave it once the review is posted.

  ```bash
  git switch <previous-branch>
  git branch -D pr-<n>
  ```

- Take `<previous-branch>` from the branch you recorded before the checkout.
- Use `-D`. The host has not merged the PR into `<previous-branch>`, so `-d` refuses the
  deletion.
- When the deletion is blocked, leave the branch in place and tell the user. The user
  deletes it.
