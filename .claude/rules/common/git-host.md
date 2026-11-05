# Git host

This file is the same in every repository that copies `.claude`. A fact specific to one repository lives in another rule file.

"The Git host" is the server hosting a repository remote. Pushes and CI happen
on `origin`; issue and PR/MR references usually point to the upstream repository,
which in a fork may be on a different host. Determine the platform separately
for each remote you act on.

Platforms (GitHub, Forgejo, GitLab) differ in APIs, CLI tooling, CI configuration,
reference syntax, and Markdown rendering. Before using any of these, identify the
platform and its version from the host information available to you: project
instructions first, then user-level instructions.

Do not infer the platform from the remote URL alone (self-hosted domains say
nothing about the software) or from repository contents such as `.github/` or
`.gitlab-ci.yml` (migrated repositories keep the old platform's files).
If a host is not covered by any instructions, ask before doing anything
platform-specific. The only hosts you may assume without being told are
`github.com` (GitHub), `codeberg.org` (Forgejo), and `gitlab.com` (GitLab);
each hosts the latest stable version of its platform.

To check a platform version when a feature requires it:

- Forgejo (including `codeberg.org`): `GET /api/v1/version`, usually without
  authentication. The result looks like `15.0.2+gitea-1.22.0`; only the part
  before `+` is the Forgejo version. A `-dev` version (e.g. `16.0.0-dev-798-…`)
  is a pre-release snapshot: treat it as the previous major version when
  checking minimum version requirements.
- GitLab (self-managed): `glab api --hostname <host> version` (requires
  authentication).
- GitHub Enterprise Server: `GET /api/v3/meta`; the `installed_version` field
  holds the version.
