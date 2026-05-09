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
6. Final sanity check:
   a. Execute `dotnet bv pack` to build everything, run tests, and produce build artifacts. After it runs, you should find artifacts (NuGet packages, Docker images, etc.) in the `artifacts` folder. You can inspect these artifacts to verify that they are correct and ready for release.
   b. Execute `dotnet dnx JetBrains.ReSharper.GlobalTools inspectcode --swea --severity=WARNING --output=inspect.sarif --format=Sarif --properties:Configuration=Release --no-build Buildvana.slnx --yes` to analyze the whole solution with ReSharper. Then run `dotnet run .claude/tools/inspect-sarif.cs` to summarize the report (one line per result), and address each one (with `--severity=WARNING`, every result will be at `error` or `warning` level — fix them all).
   c. Repeat from (a) until there are zero errors and zero warnings. If you have any doubts, or an error or warning that you think is a false positive, or that just won't go away, ask me.
   d. You can leave `inspect.sarif` in the repo, it's in `.gitignore` and won't be committed.
7. When you're done, you prepare the title, text , and labels for the PR.
8. I review the PR and propose edits if necessary.
9. When I approve, you post the PR using the GitHub MCP tool.

## Labels

- Do not apply `area:*` labels to issues or PRs. A CI workflow manages them automatically on PRs, and they're not important on issues until triage.

## Getting stuck

- If you get stuck on something, don't hesitate to ask me for help. It's better to ask for help than take wasteful detours. Just let me know what you're struggling with, and we can work through it together.
