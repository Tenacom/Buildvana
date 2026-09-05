# Design principles

Decisions that outlive any single issue. They say what to build, and which arguments for building something count.

## Scope: our workflows, not everyone's

Support X when we need X. A downstream consumer that might need X is not a reason. Being open source does not oblige us to accommodate every workflow. Compatibility for its own sake adds maintenance work, and Ric carries that work alone.

When Ric does not use VSTest, does not run workflow X, or does not build on platform Y, that is reason enough to drop it. Do not argue for a dual-track, fallback, or configurable design on the grounds that downstream users might need it.

This principle has two limits:

- It governs our own choices, not our consumers'. A project that consumes what we ship must not be locked into our tooling. See "Portability of what we ship" below.
- It governs features, not internal structure. It never licenses trimming a shared abstraction. See "Completeness of cross-cutting abstractions" below.

## Completeness of cross-cutting abstractions

YAGNI has two readings, and only one is valid:

- The member is not needed. Leave it out.
- The member is not called yet, and the code would build and work the same without it. This reading is not valid. Leaving the member out is how a use-case-specific assumption leaks into a component meant to be use-case-agnostic. That leak produces hard architectural bugs.

When you design a shared component, such as reporting, logging, or console output, give its contract the complete surface the domain implies. A logger gets every message level, not the three levels that today's call sites use. Omit a member only for a principled reason, never because nobody calls it yet.

Feature code stays minimal. Shared abstractions get built out in full.

A separate assembly is a boundary, not a packaging decision. It turns a use-case-specific type placed in a shared component into a compile error. Create the assembly when you draw the boundary, not when the assembly gets published.

## Portability of what we ship

A tool we ship must behave the same on any CI platform and on a developer laptop. When a tool needs authentication, identity, secrets, or any other runtime input, take it from a mechanism the tool controls end to end. That mechanism is the tool's own configuration file, with explicit environment-variable references inside config values. Do not take the input from whatever the host CI set up in the workspace.

"The runner sets it up for us" is a defect, not a feature. When a proposed fix works on one CI provider only, say so. Shelling out to native `git` to inherit the installed credential helpers has the same defect, so say so there too.

This rule is about user-visible portability. A platform adapter that exists by design is fine. A dependency on the runner environment in the core flow is not. "Tool portability" in `architecture.md` describes the incident that produced this rule.

## Conformance with the toolchain we belong to

Buildvana is a component of a .NET toolchain. Where that toolchain has settled a question of behavior, we settle it the same way, instead of deciding it on the merits. Examples:

- A console configured to suit `dotnet build` must suit `bv build` identically.
- The verbosity a user passes to `bv` reaches the `dotnet` invocations underneath.
- An existing convention is honored by the rules of whoever defined it. `NO_COLOR` follows its own rules, and `DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING` follows the .NET CLI's, down to the details where the two disagree.

Borrowed conventions are not harmonized with each other. Each belongs to whoever defined it, and making them consistent would mean obeying neither.

This rule outranks "we would have designed it better". It differs from the scope principle above: that one governs which features to build, this one governs matching established behavior in the ones we build.

Conformance is about observable behavior and the switches that govern it, never about source. Borrow a constant or a rule where it states a fact worth having. The Windows build below which a console host cannot be trusted at codepage 65001 is one such fact.

Do not transcribe an upstream implementation. A copied block needs resynchronizing against a repository we do not track, and nobody will remember to do it. Reimplement, and cite the source of the fact in a comment. Prefer a mechanism that needs no maintenance, such as catching what an unsupported platform throws, over one that lists every known platform.

Copying MIT-licensed code is a last resort. Omitting the attribution the licence requires is a violation in itself. The party best placed to detect a violation automatically, across every public repository, is also the party whose code is copied most often. The risk is out of proportion to the gain. The saving is trivial. The cost is a takedown notice, or an action against the account of a maintainer who works alone.

Facts, constants, names that must match in order to interoperate, and the shape of an interface are not the licensed expression. Use them freely, and cite the source as a reference. Everything else gets reimplemented. When copying is the only viable option, the same commit adds a `THIRD-PARTY-NOTICES.md` entry.

This rule bears on how I work. I answer a conformance question by reading upstream source, so the source is in front of me while I write the equivalent code. When I have read upstream to settle something, I say so, and I state what I took: this fact, this name. I do not emit code that happens to match theirs without saying so.

Deviate only where the convention's premise does not hold for us, and say so at the point of deviation. Example: `bv` does not copy the CLI's non-English-UI-culture gate on the console encoding switch. That gate exists to make localized CLI output render, and Buildvana is single-culture English. The reason for the switch itself is language-independent, so the switch stays.

Before answering a design question from first principles, check whether `dotnet`, MSBuild, or NuGet has answered it. Read their source instead of recalling it. The decision is usually in the guard clauses, and memory drops those.

## Glyphs never carry meaning alone

Setting a console's encoding changes what can be transmitted, not what can be drawn. The terminal still needs a font that contains the character, and nothing exposes font coverage. On a console with a raster font, `✓` and `✗` both render as the missing-glyph box. "Passed" and "failed" then look the same, which is worse than two distinct wrong characters.

State that the reader acts on, such as pass or fail, error, or warning, goes in words and colour. A glyph may decorate it. A glyph must never be the only carrier of it.

## Automation that feeds untrusted content to an LLM

Never build a workflow that feeds untrusted content into an LLM that holds a write capability, however narrow. Untrusted content includes PR titles, bodies, diffs, comments, and branch contents. Flag the design instead of iterating on it. Propose on-demand invocation instead: an `@claude` mention, or a maintainer-run CLI review.

Two independent reasons:

1. **Prompt injection cannot be patched away.** Untrusted input plus a write capability is a prompt-injection target. Tool lockdown, permission scoping, and input sanitization treat symptoms. No filter catches every jailbreak, and a single jailbreak turns the write capability against the repository.
2. **Generic LLM review has low signal.** An auto-triggered review action does not load `CLAUDE.md` or `.claude/rules/`, so it emits generic style feedback with no project specificity. We have seen this with Copilot review: "every time the same ado about nothing".

When such a workflow exists and is broken, weigh removing it against fixing it. Do not default to a fix. The rule covers more than PR review: flag any auto-trigger that feeds external content to an LLM with write tools.
