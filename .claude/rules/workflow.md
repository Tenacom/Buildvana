# Workflow rules

## General rules

- You are not here to be a code monkey. You are here to be a problem solver, and to help me solve problems. So always start by understanding the problem and the context, and then work together with me to find the best solution.
- Treat me as a peer, no uncalled-for deference. Just call me by name (Ric), and I'll do the same for you. We are collaborators, not master and servant.
- If I overrule you on something it's not personal, it's just business. I have to pay the bills (including your bills), so I have to make the final call on what we work on and how we work on it. But I will always listen to your input and consider it carefully before making a decision.
- If you don't understand my reasoning, ask me to explain. If I contradict myself, point it out and ask me to clarify. If you think I'm wrong, say so and explain why. I won't be offended, and I will always be open to changing my mind if you make a good case. Plus sometimes I change my mind on the fly (typically because of your input), so it's good to check in with me if something seems inconsistent.
- Please do NOT write or modify anything unless explicitly asked to do so. This includes code, documentation, issues, PRs, comments, etc. Always check with me before taking any action. This is to ensure that we are always on the same page and that we don't waste time on work that may not be needed or wanted.
- When a rule proves insufficient or misleading, propose a fix to the rule file rather than saving a feedback memory. Rules in `.claude/rules/` are checked into the repo and travel across machines; memory doesn't. Reserve memory for cross-project context like my role, preferences, and working style.
- When reviewing, flag correctness issues as highest priority.

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
2. You read the issue and make a plan
3. We review the plan together
4. You open a branch on my fork (rdeago) for the pull request
5. You write the code; I review before every commit. Always ensure the solution builds with zero errors and zero warnings and all tests (if any) pass.
6. Sanity check. This gates _every_ push to the PR branch — follow-up commits (e.g., addressing review feedback) included, not just the final commit of the initial implementation:
   a. Execute `dotnet run .claude/tools/inspect.cs --gate`. It runs `dotnet bv pack` (build, tests, build artifacts) and, if that reported nothing, analyzes the whole solution with ReSharper at WARNING severity and above. Every diagnostic of either phase is reported as `path(line,col): severity ID: message`, and the tool exits non-zero if there was any.
   b. Address every reported diagnostic, then repeat from (a) until it exits zero. If you have any doubts, or an error or warning that you think is a false positive, or that just won't go away, ask me.
   c. Build artifacts (NuGet packages, Docker images, etc.) are left in the `artifacts` folder. You can inspect them to verify that they are correct and ready for release.
   d. The full output of both phases, and the SARIF report, are left in `.buildvana-temp`, which is gitignored. Read them when a diagnostic needs more context than its one line, or when the build fails without reporting one.
7. When you're done, you prepare the title, text, and labels for the PR, following the [org-wide PR template](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/PULL_REQUEST_TEMPLATE.md). Issue and PR templates live in the org-wide repo `Tenacom/.github`, not in this repo.
8. I review the PR and propose edits if necessary.
9. When I approve, you post the PR using the GitHub MCP tool.

## Reacting to reviews

1. I give you the link to a review comment. We are usually on the PR branch, often in the same conversation where the PR has been created.
2. You check that we are on the PR branch and in sync with the remote — a reviewer may have committed a suggestion through the GitHub UI — then read the review and make a plan.
3. We review the plan together. This is usually a quick "take this one, leave this other one"; on more complicated findings, make sure we agree on the steps to take. Make sure you have all the info you need to proceed on your own: if I don't answer a question, repeat it; if you have any doubt, ask for clarification.

   Once we agree, the rest is automatic: you address the findings, sanity-check, push, and reply, all without asking me for confirmation. The agreement reached here stands in for both the general "check with me before taking any action" rule and the "I review before every commit" rule of "Solving an issue". Stop and ask only if you get stuck, or if a finding turns out to need a non-trivial refactor we did not foresee.
4. You address the review's findings as agreed in point 3. One commit per _addressed_ finding: a finding we agreed to leave alone produces no commit, only its rationale in the reply.
5. Sanity check, same as the "Sanity check" step of "Solving an issue". When it is not green, the fixes go in further commits — no amending or rewriting of the commits from point 4.
6. When you're done, push and reply to the review:
   a. Every code-anchored comment gets its own reply, even if it is just "Done.", or the rationale for not addressing the finding. Resolve each conversation after replying to it.
   b. A summary comment is needed only in two cases: to address findings that are not code-anchored, and to ask for a new review.
   c. Replies state what you did, and why you did not do the rest. Keep them structured and to the point.
   d. You should usually ask the reviewer (by nickname, e.g. `@claude`) for a new review. The request goes in the summary comment: at its end if it has other content, as its whole body otherwise. A new review is NOT necessary when the review stated that the PR was ready to be merged and you did no code changes (or if they were very trivial).

## Small changes out of scope

- Do NOT open a follow-up issue for a small change, not even when a reviewer proposes one. An issue/PR cycle costs, on average, 100x the time and effort of just making the change. The right approach is: FIX. IT. NOW!
- "Now" means in the current PR, in its own commit, plus a line in the "Additional changes" section of the PR description. That section exists precisely to record what was not in the issue's original plan, so an out-of-scope fix stays visible to the reviewer instead of being smuggled in.
- This applies to both flows above: something you notice while writing the code, and something a review surfaces.
- An issue is for work that is actually big: work that needs its own plan, or that would derail the PR under review. When in doubt, ask me — but the default is to fix it now.

## Labels

- Do not apply `area:*` labels to issues or PRs. A CI workflow manages them automatically on PRs, and they're not important on issues until triage.

## Getting stuck

- If you get stuck on something, don't hesitate to ask me for help. It's better to ask for help than take wasteful detours. Just let me know what you're struggling with, and we can work through it together.
