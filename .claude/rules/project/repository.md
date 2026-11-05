# Repository

This file is specific to this repository. Rewrite it when `.claude` is copied to another repository. `rules/common/git-host.md` says what a Git host is and how to tell the platforms apart. This file names the hosts of this repository.

- Upstream: `Tenacom/Buildvana` on `github.com`, a GitHub host. Issues, pull requests, releases, and CI live there. It is the default target of `gh` calls.
- PR branches go to the contributor's own fork, the `origin` remote. A pull request to a GitHub repository comes from a GitHub fork, so `origin` is on GitHub too. Run `git remote -v` once per session when you need the name of the fork. Do not assume it.
- CI is GitHub Actions, with the workflows under `.github/workflows/`. Codecov reads the coverage reports CI uploads, as `pr-checks.md` says.
- `GitHost` in `tools/ProjectFacts.cs` carries the platform, set to `GitHub`.
