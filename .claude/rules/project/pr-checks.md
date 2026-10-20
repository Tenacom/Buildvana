# PR checks

This file is specific to this repository. Rewrite it when `.claude` is copied to another repository. The skill `review-pr` and the self-review cycle of `workflow.md` read it when a check of a pull request fails.

Each section below covers one check, or one group of checks, whose failure does not stop a review by itself. A check with no section stops a review when it fails.

## `codecov/patch` and `codecov/project`

Codecov posts the two checks after the workflow `ci.yml` uploads the coverage reports. `codecov/project` fails when the coverage of the whole solution decreases. `codecov/patch` fails when the lines the PR changes are covered below the target. A reformat that removes covered lines fails both, so a failure of either does not stop a review by itself.

The source for both checks is the Codecov REST API. The repository is public, so the API needs no token. Read it with `curl`, and filter the output with `jq`.

When either check fails, or both do:

- Read the comparison of the PR.

  ```bash
  curl -s "https://api.codecov.io/api/v2/github/<owner>/repos/<repo>/compare/?pullid=<n>"
  ```

- The response holds `head_commit`. When it differs from `headRefOid`, wait and read again, as for a pending check. Codecov replaces the comparison at every upload.
- The response holds one entry per file of the solution in `files`. An entry has the path in `name.head`, and the counts for the lines the PR changed in `totals.patch`. A file with uncovered patch lines has a non-zero sum of `misses` and `partials` there.

  ```bash
  jq -r '.files[] | select(.totals.patch != null and (.totals.patch.misses + .totals.patch.partials) > 0) | .name.head'
  ```

- List the files the PR adds.

  ```bash
  git diff --diff-filter=A --name-only <remote>/<base>...HEAD
  ```

- An added file with uncovered patch lines breaks the first rule of `testing.md`: code gets tested. Read the lines of the file.

  ```bash
  curl -s "https://api.codecov.io/api/v2/github/<owner>/repos/<repo>/compare/file/<path>?pullid=<n>"
  ```

- The response holds one entry per line in `lines`. An entry has the line number in `number.head`, and the coverage of the line in `coverage.head`: `0` for a hit, `1` for a miss, `2` for a partial, and `null` for a line with no code. A partial counts as uncovered.

  ```bash
  jq -c '[.lines[] | select(.coverage.head == 1 or .coverage.head == 2) | .number.head]'
  ```

- Read the code at those lines, and name what the untested path does. Report one finding per added file, anchored at the uncovered lines.
- A changed file with uncovered patch lines, and a decrease of the project coverage, produce no finding, whatever their size. The lines the PR did not add are outside its scope.
- The author sees the failed check on the PR page. Do not mention an ignored failure in the review.
