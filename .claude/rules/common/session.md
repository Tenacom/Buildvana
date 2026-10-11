# Session rules

This file is the same in every repository that copies `.claude`. A fact specific to one repository lives in another rule file.

These rules hold in every session, whether or not it follows a workflow of `workflow.md`.

## Scratchpad

- Every temporary file goes under `.claude/scratchpad/`, never in a scratchpad directory the harness declares outside the repository.
- The work on an issue uses `.claude/scratchpad/<N>/`, so that sessions on different issues do not collide. Every temporary file of the issue goes there: commit messages, PR text, review replies, proof scripts, research results, and every other temporary file.
- Work without an issue uses `.claude/scratchpad/<name>/`. `<name>` is a few words joined by hyphens that describe the work. When the name is not obvious, ask me.
- The request of a review goes to `.claude/scratchpad/reviews/YYYY-MM-DD_HH.mm.ss_<PR>.json`, also in the work on an issue. The timestamp is the local time at the start of the review, from `date +%Y-%m-%d_%H.%M.%S`. `<PR>` is the number of the PR, without leading zeros. Each review gets its own file, and no review overwrites the file of another.

## Handoff files

A handoff file lets work continue in a new session, or after a context compaction.

- Work that spans more than one session, or must survive a compaction, has a handoff. When work looks like it will need one, propose it.
- The handoff of an issue is `.claude/handoff/<N>-<slug>.md`. The handoff of other work is `.claude/handoff/<name>.md`, with the `<name>` of its scratchpad.
- Write the handoff for a session that has the repository and the file, nothing else. Mark every fact as measured or decided, with the date.
- Update the handoff after every step that changes the state of the work. I then never have to ask before a compaction or at the end of a session, and a blackout costs nothing.
- When I ask to continue work that has a handoff, read the handoff in full before anything else.

## Git

`.claude/.gitignore` ignores `scratchpad/` and `handoff/`. Nothing in either directory reaches a commit.
