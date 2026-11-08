# Repository

This file is specific to this repository. Rewrite it when `.claude` is copied to another repository. `rules/common/git-host.md` says what a Git host is and how to tell the platforms apart. This file names the hosts of this repository.

- Upstream: `Tenacom/Buildvana` on `github.com`, a GitHub host. Issues, pull requests, releases, and CI live there. The host file is `.claude/hosts/github.md`, and its client resolves the repository from the remotes of the clone.
- PR branches go to the contributor's own fork, the `origin` remote. A pull request to a GitHub repository comes from a GitHub fork, so `origin` is on GitHub too. Run `git remote -v` once per session when you need the name of the fork. Do not assume it.
- CI is GitHub Actions, with the workflows under `.github/workflows/`. Codecov reads the coverage reports CI uploads, as `pr-checks.md` says.
- `GitHost` in `tools/ProjectFacts.cs` carries the platform, set to `GitHub`.
- The labels come from `manage-your-labels.yml` of `Tenacom/.github`, one set for every repository of the organization, with `:` as the scope separator. The `label-pull-requests.yml` workflow applies the `area:*` labels to a pull request, from the paths `.github/labeler.yml` maps to each label. Every other label, and every label of an issue, is applied by hand.
- The issue templates and the PR template live in the org-wide repository `Tenacom/.github`, not in this repository:
  - [Bug report](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/ISSUE_TEMPLATE/01_bug_report.yml)
  - [Enhancement proposal](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/ISSUE_TEMPLATE/02_enhancement_proposal.yml)
  - [Documentation issue](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/ISSUE_TEMPLATE/03_doc_issue.yml)
  - [Documentation request](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/ISSUE_TEMPLATE/04_docs_request.yml)
  - [PR template](https://raw.githubusercontent.com/Tenacom/.github/refs/heads/main/.github/PULL_REQUEST_TEMPLATE.md)
