# Design principles

This file is the same in every repository that copies `.claude`. A fact specific to one repository lives in another rule file.

Decisions that outlive any single issue. They say what to build, and which arguments for building something count.

## Scope: our workflows, not everyone's

Support X when we need X. "We" means every Tenacom project, present and to come, and not the one project that needs the change at hand. A downstream consumer that might need X is not a reason. Being open source does not oblige us to accommodate every workflow. Compatibility for its own sake adds maintenance work for no real benefit.

When no Tenacom project uses VSTest, runs workflow X, nor builds on platform Y, that is reason enough to drop it. Do not argue for a dual-track, fallback, or configurable design on the grounds that downstream users might need it.

This principle has three limits:

- It governs our own choices, not our consumers'. A project that consumes what we ship must not be locked into our tooling.
- It governs features, not internal structure. It never licenses trimming a shared abstraction. See "Completeness of cross-cutting abstractions" below.
- It decides whether we build a feature, not how complete the feature is. See "Plan past the issue" below.

## Plan past the issue

An issue states the use case of the person who opened it, and often nothing else. A library or a tool serves users with other use cases, and nobody can foresee which Tenacom project will need what. Those users expect to find what belongs next to a feature.

- The plan of an issue covers the most common needs around the feature, whether or not the issue names them.
- A feature comes with its obvious counterpart. A method that tests the start of a string comes with the one that tests the end.
- A feature gets the support its neighbors have. Where the string methods of a library also accept character spans, a new string method does too.
- The same holds for defaults, overloads, edge cases, and tests beyond the acceptance criteria.
- The plan states what it adds to the issue. A test beyond the acceptance criteria needs no defence. A public API beyond the issue is a proposal, which the maintainer agrees on with the plan.
- The limit is the feature the issue is about. A feature that no user would look for next to it stays out, as "Scope" above says.

## Completeness of cross-cutting abstractions

YAGNI has two readings, and only one is valid:

- The member is not needed. Leave it out.
- The member is not called yet, and the code would build and work the same without it. This reading is not valid. Leaving the member out is how a use-case-specific assumption leaks into a component meant to be use-case-agnostic. That leak produces hard architectural bugs.

When you design a shared component, such as reporting, logging, or console output, give its contract the complete surface the domain implies. A logger gets every message level, not the three levels that today's call sites use. Omit a member only for a principled reason, never because nobody calls it yet.

Feature code stays minimal. Shared abstractions get built out in full.

A separate assembly is a boundary, not a packaging decision. It turns a use-case-specific type placed in a shared component into a compile error. Create the assembly when you draw the boundary, not when the assembly gets published.

## Code from other projects

Do not transcribe the implementation of another project. A copied block needs resynchronizing against a repository we do not track, and nobody will remember to do it. Reimplement, and cite the source of the fact in a comment.

Copying MIT-licensed code is a last resort. Omitting the attribution the licence requires is a violation in itself. The party best placed to detect a violation automatically, across every public repository, is also the party whose code is copied most often. The risk is out of proportion to the gain. The saving is trivial. The cost is a takedown notice, or an action against the account of a maintainer who works alone.

Facts, constants, names that must match in order to interoperate, and the shape of an interface are not the licensed expression. Use them freely, and cite the source as a reference. Everything else gets reimplemented. When copying is the only viable option, the same commit adds a `THIRD-PARTY-NOTICES.md` entry.

A file copied or adapted from another project says so in a block comment, between the copyright preamble and the first `using` directive. For a partial type, the block goes in the main file. Use this template:

```csharp
/*
 * This file is adapted from <project>, under the <license name> license.
 * Original source: <URL of the file, at a tag or a commit>
 * Original license: <URL of the license file, at the same tag or commit>
 * THIRD-PARTY-NOTICES.md, in the project root, holds the copyright notice and the license text.
 */
```

The block is a `/* ... */` comment because StyleCop SA1512 rejects a `//` comment followed by a blank line.

This rule bears on how I work. I answer a question about the behavior of another project by reading its source, so the source is in front of me while I write the equivalent code. When I have read that source to settle something, I say so, and I state what I took: this fact, this name. I do not emit code that happens to match theirs without saying so.

## Glyphs never carry meaning alone

Setting a console's encoding changes what can be transmitted, not what can be drawn. The terminal still needs a font that contains the character, and nothing exposes font coverage. On a console with a raster font, `✓` and `✗` both render as the missing-glyph box. "Passed" and "failed" then look the same, which is worse than two distinct wrong characters.

State that the reader acts on, such as pass or fail, error, or warning, goes in words and colour. A glyph may decorate it. A glyph must never be the only carrier of it.

## Automation that feeds untrusted content to an LLM

Never build a workflow that automatically feeds untrusted content into an LLM that holds a write capability, however narrow. Untrusted content includes PR titles, bodies, diffs, comments, and branch contents. Flag the design instead of iterating on it. Propose on-demand invocation instead: a review the maintainer starts from a session, as the `review-pr` skill does.

Two independent reasons:

1. **Prompt injection cannot be patched away.** Untrusted input plus a write capability is a prompt-injection target. Tool lockdown, permission scoping, and input sanitization treat symptoms. No filter catches every jailbreak, and a single jailbreak turns the write capability against the repository.
2. **Generic LLM review has low signal.** An auto-triggered review action usually does not load `CLAUDE.md` or `.claude/rules/`, so it emits generic style feedback with no project specificity. We have seen this with Copilot review: "every time the same ado about nothing".

When such a workflow exists and is broken, weigh removing it against fixing it. Do not default to a fix. The rule covers more than PR review: flag any auto-trigger that feeds external content to an LLM with write tools.
