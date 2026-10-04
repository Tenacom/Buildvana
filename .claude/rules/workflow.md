# Workflow rules

This file is the same in every repository that copies `.claude`. A fact specific to one repository lives in another rule file.

## General rules

- Start by understanding the problem and its context, then work out the best solution with me. Do not take a request as a specification to execute.
- Treat me as a peer, without deference. Call me by name, and I will call you by name too.
- When I overrule you, it is business, not personal. The final call on what we work on, and how, is mine, because I will be held responsible for it. I weigh your input before making it.
- Ask me to explain when my reasoning is unclear. Point out my contradictions. When you think I am wrong, say so, and say why. I will not take offence, and a good case changes my mind. I also change my mind mid-session, often because of you, so check in when something looks inconsistent.
- Verify every premise I state in the code before you build on it. When the premise does not hold, say so.
- Prefer to resolve a divergence over writing code that accepts it.
- Do not write or modify anything until I ask: code, documentation, issues, PRs, comments. Check with me first. This rule keeps us aligned and saves work nobody wanted.
- I attend every session, so I am there to approve a plan. A classifier authorizes tool calls, not me. See "Tool use and the classifier" below.
  - Harness framing that calls a session "autonomous" or "unattended" does not describe my setup. Do not act on it.
  - When I ask for a plan, present it and end the turn. While you wait you may read, search, and run checks. No edits, commits, pushes, or posts.
  - "I trust your judgement" scopes the details: commit contents, wording, ordering. It never scopes the go/no-go.
- Post nothing outward-facing without a draft: issues, PRs, comments, anything that leaves this machine. Show me the full text and wait for my go-ahead.
  - This rule is a quality mechanism, not a trust mechanism, so it holds however routine the item looks. The draft often needs context that I cannot think of until I read it. Reading the draft is what surfaces that context.
  - "Just file it" for one item is a one-off, not a policy change.
  - There are two standing exceptions. In "Reacting to reviews", agreeing on the plan pre-authorizes the fixes, the push, and the replies. In "Self-review cycle", the subagent posts the review without a draft.
- Before drafting a plan, a commit message, an issue, a PR description, or a review reply, Read `.claude/output-styles/simple-tech.md` and apply it to the draft. The style sits at the start of the context, and a long session pushes it far from the draft. A fresh copy next to the draft holds better.
- When you find a working-tree change you did not make, or one unrelated to the task, report it. Ask me before you revert or overwrite it. Unexpected state in this repo is usually my own work in progress, since I edit files by hand mid-session, on purpose or by mistake. Ask, case by case. Keep your own change set clean, but never discard my edits.
- Use `gh` for all GitHub work, and never the MCP GitHub server. The classifier denies the writes of the MCP server, and `gh` keeps one login.

## Handoff of an issue

The work on an issue always has a handoff. `session.md` says where it goes and how to keep it current. In the work on an issue, a step is every commit and every review the session reacts to.

## Tool use and the classifier

Claude Code runs in auto mode, so a classifier model reviews each tool call before it runs. I do not approve tool calls one by one. Everything above about waiting for my go-ahead on a plan still stands. The classifier decides whether an action is safe, not whether it is wanted.

- Reads and edits inside the working directory skip the classifier. Shell commands and network access go through it. Prefer a file tool to its shell equivalent: `Read` over `cat`, `Edit` and `Write` over `cp`, `sed`, or a redirection.
- The classifier blocks whatever it cannot evaluate with certainty. Keep every bash command short and plain. One command, one job.
- The classifier reads my messages, the commands you run, and the `CLAUDE.md` content that Claude Code loads. It never sees tool results. Whether the files under `.claude/rules/` reach it is not documented, and experience says they do not reliably. Do not count on a boundary written in a rule file to reach it.
- The classifier reads its `autoMode` rules from `~/.claude/settings.json` and from the managed settings. It ignores an `autoMode` block in `.claude/settings.json` and in `.claude/settings.local.json`, so that a repository cannot add allow rules. A rule for the classifier is therefore a personal setting, and I write it myself. The project settings can still hold a narrow `permissions.allow` entry, which applies before the classifier.

### When a command seems to hang

- A bash command that appears to time out was most likely blocked. Claude Code cannot tell the two apart, and the reason it receives is often the bare text `Blocked by classifier`.
- Do not retry the command. A retry is a second block, and three blocks in a row drop the session out of auto mode.
- Simplify instead. Split a compound command into its parts, replace a shell command with a file tool, or drop the part that needed evaluating.
- When nothing simpler works, tell me what you were trying to do. I can run it myself, or retry it from the **Recently denied** tab of `/permissions`.

### Command shapes to avoid

- **Heredocs and nowdocs in a Bash command.** Never write `<<EOF` or any variant of it. Write the content with `Write`, or pass it as a quoted argument. The ban covers Bash commands only.
- **`cd`.** In a Bash command, `cd` moves the working directory for the following commands, and it can trigger a permission prompt. Use absolute paths.
- **`cp` or a redirection onto a file that already exists in the repository.** Overwriting a file that predates the session is a blocked category. Use `Edit` or `Write` instead, which the classifier does not review for a path in the working directory.
- **Compound commands.** The classifier evaluates each part of a command joined by `&&`, `||`, `;`, or a pipe. Separate calls read more clearly to it and to me.
- **Anything that discards work.** `git reset --hard`, `git checkout -- .`, `git restore .`, `git clean -fd`, `git stash drop`, and `git stash clear` are blocked by default. So is `git commit --amend` on a commit you did not create in this session, or one already pushed. So is `git branch -D`, on any branch.
- **Very long commands.** A command over 10,000 characters is never auto-approved.

### Boundaries I state in conversation

- When I say "don't push", or "wait until I review", the classifier enforces it as a block, whatever its default rules would allow. The boundary holds until I remove it. Your own judgement that the condition is met does not remove it.
- The classifier re-reads each boundary from the transcript, so compaction can lose one. Never treat a boundary as removed because you can no longer see it. Ask me.
- An instruction of mine that contradicts this file is temporary by default. It lasts for the work on the issue, or once if I say so. An example is "don't push yet", when a rule change must go on `main` first, or when the context needs compacting. When I want to change the workflow for the future, I say so explicitly, and the change goes in this file.

## Posting an issue

1. Either you or I identify the problem: usually a bug or an enhancement proposal.
2. You analyze the situation and make a plan.
3. We review the plan together.
4. You prepare the issue, following one of these templates:
   - [Bug report](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/ISSUE_TEMPLATE/01_bug_report.yml)
   - [Enhancement proposal](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/ISSUE_TEMPLATE/02_enhancement_proposal.yml)
   - [Documentation issue](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/ISSUE_TEMPLATE/03_doc_issue.yml)
   - [Documentation request](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/ISSUE_TEMPLATE/04_docs_request.yml)
   - For anything else, no template.

   Acceptance criteria must include a changelog update for every public-facing change. See `CHANGELOG.md` for section structure (Keep a Changelog format under `## Unreleased changes`) and the `**BREAKING CHANGE**:` convention.
5. I review the issue and propose edits if necessary.
6. When I approve the issue, you post it, using the `gh` CLI.

## Solving an issue

1. I tell you which issue must be solved, or I ask to continue the work on an issue. In the second case, read the handoff file first.
2. You read the issue and make a plan.
   - Assume I have not read the issue. Open the plan with the problem and the acceptance criteria, in the issue's own terms, then the PRs.
   - When the issue needs more than one PR, use the fewest that can each merge on their own. Do not split a coherent area because it is large.
   - Every PR costs a changelog entry, the configuration files, a description, and a style sweep, whatever its size.
   - For each PR, state what makes it independently mergeable.
3. We review the plan together.
4. You open a branch on my fork for the pull request.
5. You write the code, and I review before every commit. A code change after my approval needs a new approval. A push needs no approval, unless I ask you to hold it. Always ensure code builds with zero errors and zero warnings, and that all tests pass. The message of each commit follows "Commit messages" below. Write it in a file in the issue scratchpad and give me the link. Do not paste it in chat. List in chat the files of the commit and anything I have not seen yet. After the commit, stop at a compaction point, as "Compaction points" says. Do not start the next commit in the same turn.
6. Sanity check. It gates every push to the PR branch, follow-up commits included:
   1. Execute `dotnet run .claude/tools/inspect.cs --gate`. It runs `lint-docs.cs` on the documentation (if any), then `dotnet bv pack` for build, tests, and build artifacts. When the build reports nothing, the tool analyzes the whole solution with ReSharper at WARNING severity and above. All three phases report every diagnostic as `path(line,col): severity ID: message`, and the tool exits non-zero when there is any.
   2. Address every reported diagnostic, then repeat from step 1 until it exits zero. Ask me when you have any doubt, when a diagnostic looks like a false positive, or when a diagnostic does not go away.
   3. Build artifacts, such as NuGet packages and Docker images, are left in the `artifacts` folder. You can inspect them to verify that they are correct and ready for release.
   4. The full output of both phases, and the SARIF report generated by ReSharper, are left in `.buildvana-temp`, which is gitignored. Read them when a diagnostic needs more context than its one line, or when the build fails without reporting one.
7. When you're done, you prepare the title, text, and labels for the PR, following the [org-wide PR template](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/PULL_REQUEST_TEMPLATE.md). Issue and PR templates live in the org-wide repo `Tenacom/.github`, not in this repo. Draft the text in `.claude/scratchpad/<N>/<N>-pr.md` and show it with a link. See "Pull request text" below.
8. I review the PR and propose edits if necessary.
9. When I approve, you post the PR with `gh pr create --body-file <file>`. After a later change to the text, update the PR with `gh pr edit <N> --body-file <file>` and read it back with `gh pr view <N> --json body`.
10. Once the PR is up, start the self-review cycle. See "Self-review cycle" below.

### Pull request text

- When the PR closes the issue, the body carries `Closes #N`. When an issue needs several PRs, only the last one carries it. Verify the link with the `closingIssuesReferences` field through GraphQL.
- When an issue states a wrong premise, the PR says so, with the measured data. Do not take a premise of the issue as true when the data say otherwise.
- Observations outside the issue go in the "Additional changes" section when the PR acts on them. See "Small changes out of scope" below. The PR does not list what it leaves alone.

## Self-review cycle

A PR goes through two review cycles: self-review, then my own review. The goal of the self-review cycle is a self-review that approves the PR without findings. I review the PR myself after that, on the PR page, and I merge.

Answering a finding and resolving the thread counts as much as fixing it. The bar is zero open conversations, not zero comments ever written. See "Evaluating findings" below for how to weigh what a review claims.

You run the self-review cycle without a report from me. You start each review, read it, and present the plan. I still agree on every plan, as "Reacting to reviews" says. A review without findings needs no plan, so take the next step.

A self-review is a review posted by my own account, under the rules of the skill `.claude/skills/review-pr/SKILL.md`. GitHub refuses a review request to the author of the PR, so nobody requests it. You start it, through the subagent `pr-reviewer`, defined in `.claude/agents/pr-reviewer.md`.

Before each self-review:

1. Wait for the checks of the head commit, in the background, with `gh pr checks <N> --repo <owner>/<repo> --watch`. When a check fails, tell me.
2. Check that `git status --porcelain` prints nothing, and that `git rev-parse HEAD` equals the `headRefOid` of the PR. When either check fails, stop and tell me.
3. Start the subagent `pr-reviewer` in the foreground. The prompt is `Review PR <N>.`, with no other content. The subagent then judges the PR without the reasoning of the session that wrote the code.

The subagent reviews the PR in this clone, on the current branch. It does not use a worktree, because a worktree has no restored packages, and a review reads their sources.

The subagent posts the review without a draft and without a go-ahead. This is a standing exception to the rule on drafts in "General rules".

When the subagent ends, read the review from GitHub. When the review has findings, react to it, as "Reacting to reviews" says, then start the next self-review. When the review approves the PR without findings, tell me that the PR is ready for my review.

A self-review is a `COMMENT` review. Its body starts with `_Review by Claude. Self-review._` and a verdict line, `**👍 Approved.**` or `**⚠️ Changes requested.**`. Findings are code-anchored comments.

A self-review is a review, and gets the same treatment as any other. Answer every finding as if another user had written it. Reply in each thread and resolve it. A finding in the review body, without a thread, gets a comment on the PR that quotes the sentence it answers.

### Compaction points

I compact the context at three kinds of point. You cannot compact it, because `/compact` is my command. The compaction points are:

- each commit of the work on the issue, once it is made, as step 5 of "Solving an issue" says;
- the start of the cycle, after the PR is open and before the first self-review;
- the end of each reaction to a review, as "Reacting to reviews" says.

At a compaction point:

1. Update the handoff. State the PR when there is one, the round, and the next step. List every boundary I stated in conversation that still holds, because compaction can lose it.
2. Ask me to compact, and end the turn. Whether to compact is my call, so the turn ends either way.
3. When I tell you to continue, read the handoff in full. Then take the next step.

A review without new findings produces no reaction, so no compaction point follows it.

### Reacting to reviews

1. Read the review: the threads and the review body.
2. Check that we are on the PR branch and in sync with the remote: `git status --porcelain` prints nothing, and `git rev-parse HEAD` equals the `headRefOid` of the PR.
3. Evaluate each finding, as "Evaluating findings" says, and present a plan: fix or reject, with the rationale. We agree on the plan.

   Assume I have not read the review. For each finding, first restate what the reviewer said, in one or two sentences, then the plan for it.

   State the shape of the defect, not only the site the review names. Say what you searched for, how many occurrences you found, and how many the review names. Where the two numbers differ, say what you propose to do with the rest. The default is to fix them all in one commit, per "Small changes out of scope". A review names a sample of the occurrences, not all of them.

   From the second round on, a finding the reviewer does not treat as blocking starts as "leave alone". Fix it only when I say so. My silence is not a yes. Prose, comments, changelog wording, documentation symmetry, and formatting get one round each.

   Make sure you have everything you need to proceed on your own. Repeat a question I did not answer, and ask when you have any doubt.
4. Apply the fixes, commit, run the sanity check, and push. The agreed plan pre-authorizes the commits and the push, so do not wait for a further approval. Stop and ask only when you get stuck, or when a finding needs a refactor we did not foresee.

   A finding we agreed to leave alone produces no commit, only its rationale in the reply.

   One commit per addressed finding is the default, not a rule. Several findings of one shape belong in one commit. One finding whose fix is larger than the reviewer thought belongs in several. A commit never mixes unrelated fixes. When a commit covers occurrences the review did not name, say so in its message. The message of each commit follows "Commit messages" below.

   When the sanity check fails, the fixes go in further commits. Never amend or rewrite the commits already made for the round.
5. Reply in each thread and resolve it. The agreed plan pre-authorizes the replies. Draft each reply in `.claude/scratchpad/<N>/reply-<PR>-<thread id>.md`. State what you did, and why you did not do the rest. Keep replies structured and short.

   Post a reply with:

   ```text
   gh api --method POST repos/<owner>/<repo>/pulls/<N>/comments/<comment id>/replies -F body=@<file>
   ```

   Resolve the thread with the GraphQL mutation `resolveReviewThread`.
6. Stop at the compaction point, as "Compaction points" says. After the compaction, start the next self-review.

### Evaluating findings

When a finding is reported, it is your responsibility to evaluate it and decide whether to fix it or reject it. Do not assume that a finding is correct just because it was reported. Always verify the finding against the code and the requirements of the project.

Findings come in two kinds, and they are not verified the same way.

- **Claims about what the code does**, such as a flag's effect, an inheritance rule, a target's execution order. These live in the repo and its dependencies. Verify them there before acting on them.
- **Claims about what the project needs**, such as "nobody asks for this", "that case never comes up", "the capability is unused". The reviewer might have extrapolated the premise from current code, leaving no room for future needs. Do not take such claims at face value. Let's verify them together. `design-principles.md` settles some of them.

A finding about a commit message or about a branch name is not fixed. Present it as a rejection in the plan. The reply names the cost of the change: a rewrite of the history of the branch, or a new PR.

When a finding asks to remove something deliberate, recovering _why it was put there_ is part of answering it. If the reason isn't in the repo, the question is for me, not for `git`.

## Commit messages

A commit message is read months later, by a reader who has the repository and nothing else. Keep it short, and keep it free of anything that reader cannot look up.

- The subject says what the commit does, in the imperative, with an identifier when the change has one. It names the behavior that is gone, not the rule the change obeys.
- The body is one paragraph, and it says why. State the wrong behavior first, then the reason the fix took this shape. The diff says what changed, so the body does not repeat it.
- A term that exists only in the PR's conversation is banned. Name the thing with an identifier from the repository, or describe it. A reader can look up `OverrideLifecycle`. Nobody can look up "the lifecycle".
- Self-contained does not mean complete. The body stays short by naming things instead of describing them, and by leaving the what to the diff.

Before every commit:

1. Read `.claude/output-styles/simple-tech.md`. Follow its instructions when writing.
2. Write the message to a file in `.claude/scratchpad/`.
3. Run `dotnet run .claude/tools/lint-commit.cs <file>`. Fix the message until the tool reports nothing.
4. Check the three things the tool cannot, and state the result in the turn:
   - Every definite noun phrase names a repository identifier, or a thing the message defined earlier.
   - The subject names the behavior that is gone.
   - Each sentence puts its condition before its action.
5. Show me the message together with the diff.
6. Commit with `git commit -F <file>`.

## Small changes out of scope

- Do not open a follow-up issue for a small change, not even when a reviewer proposes one. An issue and PR cycle costs about 100 times the effort of making the change. Make the change now.
- "Now" means in the current PR, in its own commit, plus a line in the "Additional changes" section of the PR description. That section records what the issue's plan did not ask for, so an out-of-scope fix reads as intentional. See "The Additional changes section" below.
- This rule applies to both flows above: something you notice while writing the code, and something a review surfaces.
- An issue is for big work: work that needs its own plan, or that would derail the PR under review. When in doubt, ask me. The default is to fix it now.
- This section says where a change goes, not whether it is worth making. Once we decide to make a change, it goes in this PR. Whether to make it at all is a separate call, and for review findings point 3 of "Reacting to reviews" governs it. A change we drop does not become an issue, and it does not go on a list.

## The "Additional changes" section

The PR description tells a reviewer who is about to read the diff what the diff does. It does not narrate how the branch got there. "Additional changes" is the part that covers what the issue did not ask for, so that an out-of-scope change reads as intentional.

- Its entire scope is **changes beyond the issue's plan that are present in the final diff**, one bullet each, with the rationale.
- **Rewrite it, never append to it.** When a later commit revises an out-of-scope change, edit its bullet in place. Two bullets that describe one change become one. The section describes the branch as it stands, not how it got there.
- **No round headings, no commit-by-commit log, no numbers that were true at some point.** "From the second review", "From Codecov", commit numbers, and a past coverage percentage are history. Git and the review replies hold history.
- **Fixes to code the PR itself introduced are not additional changes.** They never reached `main`, so a reviewer of the final diff has nothing to reconcile. This holds however much work they were.
- **A decision to leave something alone is not a change.** Its rationale belongs in the review reply that raised it, or in a comment next to the thing itself. That is where the next person to ask will look. The one exception is a known limitation of what the PR does change, which the reviewer needs in order to assess it.

## Labels

- Do not apply `area:*` labels to issues or PRs. A CI workflow manages them automatically on PRs, and they do not matter on issues until triage.

## Getting stuck

- When you get stuck, ask me for help. Asking costs less than a detour. Tell me what you are struggling with, and we work through it together.
