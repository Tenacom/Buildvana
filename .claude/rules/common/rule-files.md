# Rule files

This file is the same in every repository that copies `.claude`. A fact specific to one repository lives in another rule file.

A rule file is a Markdown file under `.claude/rules/`. A new session reads it with no knowledge of the repository, and none of the session that wrote the rule.

## A rule and its reason

- State each rule once, in one list.
- State the reason in at most three sentences. The reason names the cause, such as a defect of a tool.
- Do not tell how the decision was reached: no experiment, no measured result, no "X was tried, then reverted". The handoff and the PR text hold those.
- Do not continue a list under a second heading.
- Do not write a bullet that follows at once from another rule.

## Facts about the project

A rule that states facts about the project needs an edit whenever the facts change. Write it so that few changes of the project reach it.

- Give examples, not the list of everything the rule covers.
- Do not count things that the project may add or remove. "The two packages" is wrong when a third arrives.
- An example names a BCL type, or describes a feature. A new session does not know the types of the repository, and would have to search for them.
- Name a type of the repository only when nothing else can state the rule.
