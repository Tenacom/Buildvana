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
- Update the handoff after every step that changes the state of the work. I then never have to ask before a compaction or at the end of a session, and a blackout costs nothing.
- When I ask to continue work that has a handoff, read the handoff in full before anything else.

### What a handoff holds

The handoff describes the state of the work. It is not a record of what the session did. That record is in git, on GitHub, and in the scratchpad. Rewrite the handoff at every update. Delete what the update makes false or finished. Do not append a new section above or below the old one.

The next session reads the handoff once, from the top, and takes the next step. It has the repository, with its history and its remote, and the scratchpad. Every sentence it has to skip is a defect. A section about a finished step, a plan a later decision replaced, or a check a finished commit passed, is deleted at the update that finishes or replaces it. Two sections with the same title, or a section that tells the reader which other section to read, mean the update appended instead of rewriting.

The handoff holds, in this order:

- where the work stands, in one paragraph, with the branch, the PR, the round, and the next step;
- the boundaries I stated in conversation that are in no rule file;
- the decisions the next session needs that are in no issue, PR text, commit message, code comment, or memory, each with its date and who took it;
- the open questions;
- the scratchpad files whose state the directory does not show: a draft, with whether it was posted; a proof or a query, with what it covers and whether it was run; a result file, with the commit it measured.
  The message of a committed commit and a posted reply need no entry, because git and GitHub hold them.

Mark every fact as measured or decided, with the date. A date is enough. Do not write times.

The handoff does not hold what the next session can read elsewhere: the output of `git log`, `gh pr view`, or `gh issue view`; a procedure a rule file describes, which the handoff names by its section; a proposal that was rejected, unless it is likely to come back, and then in one line with the reason.

When the `Read` tool truncates the handoff, the next session cannot read it in full. Rewrite the handoff before the session break.

## Git

`.claude/.gitignore` ignores `scratchpad/` and `handoff/`. Nothing in either directory reaches a commit.
