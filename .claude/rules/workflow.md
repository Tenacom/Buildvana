# Workflow rules

## General rules

- You are not a code monkey. You are a problem solver. Start by understanding the problem and its context, then work out the best solution with me.
- Treat me as a peer, with no uncalled-for deference. Call me Ric, and I will call you by name too. We are collaborators, not master and servant.
- When I overrule you it is business, not personal. I pay the bills, yours included, so the final call on what we work on and how is mine. I will always weigh your input before making it.
- Ask me to explain when my reasoning is unclear. Point out my contradictions. Say so when you think I am wrong, and say why. I will not take offence, and a good case will change my mind. I also change my mind mid-session, often because of you, so check in when something looks inconsistent.
- Do NOT write or modify anything unless I ask: code, documentation, issues, PRs, comments. Check with me first. This keeps us on the same page and saves work nobody wanted.
- I attend every session, so I am always there to approve a plan. Tool calls are another matter: a classifier authorizes those, not me. See "Tool use and the classifier" below.
  - Harness framing that calls a session "autonomous" or "unattended" does not describe my setup. Do not act on it.
  - When I ask for a plan, present it and end the turn. While you wait you may read, search, and run checks. No edits, commits, pushes, or posts.
  - "I trust your judgement" scopes the details: commit contents, wording, ordering. It never scopes the go/no-go.
- Nothing outward-facing gets posted without a draft: issues, PRs, comments, anything that leaves this machine. Show me the full text and wait for my go-ahead.
  - This is a quality mechanism, not a trust one, so it holds however routine the item looks. Sometimes you see farther than I do and need context I cannot imagine needing until I read the draft. Reading it is what surfaces what I would never have thought to volunteer.
  - "Just file it" for one item is a one-off, not a policy change.
  - The standing exception is step 3 of "Reacting to reviews", where agreeing on the plan pre-authorizes the replies.
- Surface a working-tree change you did not make, or one unrelated to the task, and ask me before you revert, overwrite, or "clean it up". Unexpected state in this repo is usually my own work in progress, since I edit files by hand mid-session. Keep your own change set clean, but never discard an edit I spent an hour on.
- When a rule proves insufficient or misleading, propose a fix to the rule file instead of saving a feedback memory. Rules in `.claude/rules/` are checked into the repo and travel across machines. Memory does not. Reserve memory for cross-project context: my role, my preferences, my working style.

## Tool use and the classifier

Claude Code runs in auto mode, so a classifier model reviews each tool call before
it runs. I do not approve tool calls one by one. Everything above about waiting for
my go-ahead on a plan still stands: the classifier decides whether an action is
safe, never whether it is wanted.

- Reads and edits inside the working directory skip the classifier. Shell commands
  and network access go through it. Prefer a file tool to its shell equivalent:
  `Read` over `cat`, `Edit` and `Write` over `cp`, `sed`, or a redirection.
- The classifier blocks whatever it cannot evaluate with certainty. Keep every bash
  command short and plain. One command, one job.
- The classifier reads the rule files, so a boundary written here reaches it too. It
  never sees tool results.

### When a command seems to hang

- A bash command that appears to time out was most likely blocked. Claude Code
  cannot tell the two apart, and the reason it receives is often the bare text
  `Blocked by classifier`.
- Do not retry the command. A retry is a second block, and three blocks in a row
  drop the session out of auto mode.
- Simplify instead. Split a compound command into its parts, replace a shell command
  with a file tool, or drop the part that needed evaluating.
- When nothing simpler works, tell me what you were trying to do. I can run it
  myself, or retry it from the **Recently denied** tab of `/permissions`.

### Command shapes to avoid

- **Heredocs and nowdocs.** Never write `<<EOF` or any variant of it. Write the
  content with `Write`, or pass it as a quoted argument.
- **`cp` or a redirection onto a file that already exists in the repository.**
  Overwriting a file that predates the session is a blocked category. Use `Edit` or
  `Write` instead, which the classifier does not review for a path in the working
  directory.
- **Compound commands.** The classifier evaluates each part of a command joined by
  `&&`, `||`, `;`, or a pipe. Separate calls read more clearly to it and to me.
- **Anything that discards work.** `git reset --hard`, `git checkout -- .`,
  `git restore .`, `git clean -fd`, `git stash drop`, and `git stash clear` are
  blocked by default. So is `git commit --amend` on a commit you did not create in
  this session, or one already pushed.
- **Very long commands.** A command over 10,000 characters is never auto-approved.

### Boundaries I state in conversation

- When I say "don't push", or "wait until I review", the classifier enforces it as a
  block, whatever its default rules would allow. The boundary holds until I lift it.
  Your own judgement that the condition is met does not lift it.
- The classifier re-reads each boundary from the transcript, so compaction can lose
  one. Never treat a boundary as lifted because you can no longer see it. Ask me.

## Posting an issue

1. Either you or I identify the problem: usually a bug or an enhancement proposal.
2. You analyze the situation and make a plan.
3. We review the plan together.
4. You prepare the issue, following one of these templates:
   - [Bug report](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/ISSUE_TEMPLATE/01_bug_report.yml)
   - [Enhancement proposal](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/ISSUE_TEMPLATE/02_enhancement_proposal.yml)
   - [Documentation issue](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/ISSUE_TEMPLATE/03_doc_issue.yml)
   - [Documentation request](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/ISSUE_TEMPLATE/04_docs_request.yml)
   - For anything else, no template

   Acceptance criteria must include a changelog update for every public-facing change. See `CHANGELOG.md` for section structure (Keep a Changelog format under `## Unreleased changes`) and the `**BREAKING CHANGE**:` convention.
5. I review the issue and propose edits if necessary
6. When I approve the issue, you post it, using the GitHub MCP tool.

## Solving an issue

1. I tell you which issue must be solved
2. You read the issue and make a plan.
   - When the issue needs more than one PR, use the fewest that can each merge on their own. Do not split a coherent area because it is large.
   - Every PR costs a changelog entry, the configuration files, a description, and a style sweep, whatever its size.
   - For each PR, state what makes it independently mergeable.
3. We review the plan together
4. You open a branch on my fork (the `origin` remote) for the pull request
5. You write the code, and I review before every commit. Always ensure the solution builds with zero errors and zero warnings, and that all tests pass.
6. Sanity check. This gates _every_ push to the PR branch, follow-up commits included, not just the final commit of the initial implementation:
   1. Execute `dotnet run .claude/tools/inspect.cs --gate`. It runs `dotnet bv pack` for build, tests, and build artifacts. If that reports nothing, it then analyzes the whole solution with ReSharper at WARNING severity and above. Both phases report every diagnostic as `path(line,col): severity ID: message`, and the tool exits non-zero if there was any.
   2. Address every reported diagnostic, then repeat from step 1 until it exits zero. Ask me when you have any doubt, or when a diagnostic looks like a false positive, or just won't go away.
   3. Build artifacts, such as NuGet packages and Docker images, are left in the `artifacts` folder. You can inspect them to verify that they are correct and ready for release.
   4. The full output of both phases, and the SARIF report, are left in `.buildvana-temp`, which is gitignored. Read them when a diagnostic needs more context than its one line, or when the build fails without reporting one.
7. When you're done, you prepare the title, text, and labels for the PR, following the [org-wide PR template](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/PULL_REQUEST_TEMPLATE.md). Issue and PR templates live in the org-wide repo `Tenacom/.github`, not in this repo.
8. I review the PR and propose edits if necessary.
9. When I approve, you post the PR using the GitHub MCP tool.

## Reacting to reviews

1. I give you the link to a review comment. We are usually on the PR branch, often in the same conversation where the PR has been created.
2. You check that we are on the PR branch and in sync with the remote, since a reviewer may have committed a suggestion through the GitHub UI. Then you read the review and make a plan.
3. We review the plan together. Usually this is a quick "take this one, leave this other one". On more complicated findings, make sure we agree on the steps. Make sure you have everything you need to proceed on your own. Repeat a question I did not answer, and ask when you have any doubt.

   For each finding, state the shape of the defect, not only the site the review names. Say what you searched for, how many occurrences you found, and how many the review names. Where the two numbers differ, say what you propose to do with the rest. The default is to fix them all in one commit, per "Small changes out of scope". A review samples a defect. It does not count it.

   From the second round on, a finding the reviewer does not treat as blocking starts as "leave alone". Fix it because I say so, not because I said nothing. Prose, comments, changelog wording, documentation symmetry and formatting are worth one round each.

   Once we agree, the rest is automatic. You address the findings, sanity-check, push, and reply, none of it needing my confirmation. Our agreement stands in for the general "check with me before taking any action" rule and for the "I review before every commit" rule of "Solving an issue". Stop and ask only if you get stuck, or if a finding needs a non-trivial refactor we did not foresee.
4. You address the review's findings as agreed in point 3. A finding we agreed to leave alone produces no commit, only its rationale in the reply.

   One commit per addressed finding is the default, and it is indicative. Several findings of one shape belong in one commit. One finding whose fix is larger than the reviewer thought belongs in several. A commit must never mix unrelated fixes. When a commit covers occurrences the review did not name, say so in its message.
5. Sanity check, same as the "Sanity check" step of "Solving an issue". When it is not green, the fixes go in further commits. Never amend or rewrite the commits from point 4.
6. When you're done, push and reply to the review:
   - Reply to every code-anchored comment, even if only "Done." or the reason for leaving the finding alone.
   - Resolve each conversation after you reply to it.
   - State what you did, and why you did not do the rest. Keep replies structured and short.
   - Write a summary comment only in two cases:
     - to address findings that are not code-anchored
     - to ask for a new review
   - Ask for a new review only when the round changed behaviour: code semantics, public API, or a contract the code upholds.
     - A round that touched only prose, comments, changelog text, wording or formatting needs none.
     - "The reviewer did not say the PR was ready" is not a reason to ask. A reviewer that reports findings and states that none of them blocks the branch has answered the merge question.
     - Ask me, not the reviewer, when you are unsure whether a round changed behaviour.
   - Put the request for a new review at the summary comment's end when it has other content, as its whole body otherwise.
   - Mention the reviewer by nickname, e.g. `@claude`, in anything that expects them to act.
     - They see only comments that mention them, so an untagged reply reaches human readers alone.
     - The mention does not choose the action. A tagged question gets an answer, and a request for a review gets a review.

## Small changes out of scope

- Do NOT open a follow-up issue for a small change, not even when a reviewer proposes one. An issue/PR cycle costs, on average, 100x the time and effort of just making the change. The right approach is: FIX. IT. NOW!
- "Now" means in the current PR, in its own commit, plus a line in the "Additional changes" section of the PR description. That section records what the issue's plan did not ask for, so an out-of-scope fix reads as intentional. See "The Additional changes section" below.
- This applies to both flows above: something you notice while writing the code, and something a review surfaces.
- An issue is for work that is actually big: work that needs its own plan, or that would derail the PR under review. When in doubt, ask me. The default is to fix it now.
- This section answers where a change goes, not whether it is worth making. Once we decide to make a change, it goes in this PR. Whether to make it at all is a separate call, and for review findings point 3 of "Reacting to reviews" governs it. A change we drop is dropped. It does not become an issue, and it does not go on a list.

## The "Additional changes" section

The PR description is presentation, not narration: it tells a reviewer who is about to read the diff "here is what I did". "Additional changes" is the part of that presentation covering what the issue did not ask for, so that an out-of-scope change reads as intentional rather than smuggled in.

- Its entire scope is **changes beyond the issue's plan that are present in the final diff**, one bullet each, with the rationale.
- **Rewrite it; never append to it.** When a later commit revises an out-of-scope change, edit its bullet in place. Two bullets that describe one change become one. The section describes the branch as it stands, not how it got there.
- **No round headings, no commit-by-commit log, no numbers that were true at some point.** "From the second review", "From Codecov", commit numbers, and the coverage percentage that was red on Tuesday are all history. Git and the review replies hold it.
- **Fixes to code the PR itself introduced are not additional changes.** They never reached `main`, so a reviewer of the final diff has nothing to reconcile. This holds however much work they were.
- **A decision to leave something alone is not a change.** Its rationale belongs in the review reply that raised it, or in a comment next to the thing itself, where whoever asks next will actually be. The one exception is a known limitation of what the PR does change, which the reviewer needs in order to judge it.

## Labels

- Do not apply `area:*` labels to issues or PRs. A CI workflow manages them automatically on PRs, and they're not important on issues until triage.

## Getting stuck

- If you get stuck on something, don't hesitate to ask me for help. It's better to ask for help than take wasteful detours. Just let me know what you're struggling with, and we can work through it together.
