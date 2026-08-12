# Design principles

Cross-cutting decisions that outlive any single issue. These are about _what to build_, and about which arguments for building something are legitimate.

## Scope: our workflows, not everyone's

The bar for "should we support X?" is whether _we_ need X — not whether some hypothetical downstream consumer might.
Being open source is not an obligation to accommodate arbitrary workflows; compatibility for its own sake adds maintenance burden that Ric carries solo.

If Ric doesn't use VSTest, doesn't run workflow X, doesn't build on platform Y, that is reason enough to drop it.
Do not argue for dual-track, fallback, or "let's make it configurable" designs on the grounds that downstream users might need them.

Two limits on this principle, both easy to overreach:

- It governs _our own_ choices, not our consumers'. Projects that consume what we ship must not be locked into our tooling — see "Portability of what we ship" below.
- It governs _features_, not internal structure. It never licenses trimming a shared abstraction — see "Completeness of cross-cutting abstractions" below.

## Completeness of cross-cutting abstractions

YAGNI splits in two, and only one half is real:

- **"Genuinely not needed"** — fine to skip.
- **"It would build and work the same, so who cares"** — not fine. This is where some of the nastiest architectural bugs come from, because it is exactly how use-case-specific assumptions leak into a concern that is supposed to be use-case-agnostic.

So when designing a shared or cross-cutting component — reporting, logging, console output, and the like — give its contract the complete, symmetric surface the domain implies: the whole message-level ladder, not the three levels today's call sites happen to exercise.
Omit a member only for a principled reason, never merely because nobody calls it yet.

Feature and use-case code stays minimal. Boundaries and shared abstractions get built out fully.

A separate assembly is a _boundary_, not a packaging bet: it is the wall that turns "I'll just slip this one use-case-specific type in here" into a compile error.
Build the wall when the boundary is drawn, not if and when the assembly is ever published.

## Portability of what we ship

Tools we ship must behave the same on any CI platform and on a developer laptop.
When a tool needs authentication, identity, secrets, or any other runtime input, prefer a mechanism the tool controls end to end — its own configuration file, with explicit environment-variable references inside config values — over one that depends on how the host CI happens to have set up the workspace.

Treat "the runner sets it up for us" as a smell, not a feature. If a proposed fix would only work on one CI provider, say so out loud.
Shelling out to native `git` to inherit whatever credential helpers are installed has the same defect and gets the same flag.

This is about _user-visible_ portability. Platform adapters are fine where they exist by design; runner-environment dependencies in the core flow are not.
See "Tool portability" in `architecture.md` for the incident that produced this rule.

## Conformance with the toolchain we belong to

Buildvana is a component of a .NET toolchain. Where that toolchain has already settled a question of behavior, we settle it the same way instead of deciding it on the merits: a console configured to suit `dotnet build` must suit `bv build` identically, the verbosity a user passes `bv` reaches the `dotnet` invocations underneath, and an existing convention is honored on its owner's terms — `NO_COLOR` by its rules, `DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING` by the .NET CLI's, down to the details where the two disagree. Borrowed conventions are not harmonized with each other; each belongs to whoever defined it, and making them consistent would mean obeying neither.

This outranks "we would have designed it better". It is also not the scope principle above wearing a different hat: that one governs _which_ features to build, this one governs matching established behavior in the ones we do build.

Conformance is about observable behavior and the switches that govern it, never about source. Borrow a constant or a rule where it states a fact worth having — the Windows build below which a console host cannot be trusted at codepage 65001 is one — but do not transcribe an upstream implementation: a copied block invents a duty to resynchronize against a repository we do not track and nobody will remember to check. Reimplement, cite the source of the _fact_ in a comment, and prefer a mechanism that needs no maintenance (catching what an unsupported platform throws) over one that enumerates a snapshot of the world.

Copying MIT-licensed code is a last resort, not a shortcut. Omitting the attribution the licence requires is itself a violation, and the party best able to detect one at scale — automatically, across every public repository — is also the party whose code is most often copied. The downside is asymmetric: a trivial saving weighed against a takedown notice, or an action against the account of a maintainer who works alone. Facts, constants, names that must match in order to interoperate, and the shape of an interface are not the licensed expression: use them freely and cite the source as a reference. Everything else gets reimplemented. On the rare occasion when copying really is the only viable option, it ships with a `THIRD-PARTY-NOTICES.md` entry in the same commit.

This bears on how I work in particular: reading upstream source is how I answer conformance questions, so the copy is always in front of me exactly when I am writing the equivalent code. When I have read upstream to settle something, I say so and state what I took — this fact, this name — rather than quietly emitting code that happens to match theirs.

Deviate only where the convention's premise does not hold for us, and say so explicitly at the point of deviation. Worked example: `bv` does not copy the CLI's non-English-UI-culture gate on the console encoding switch, because that gate exists to make _localized_ CLI output render and Buildvana is single-culture English, while the reason for the switch itself is language-independent.

Before answering "what should we do here?" from first principles, check whether `dotnet`, MSBuild, or NuGet has already answered it — and read their source rather than recalling it, since the guards are usually where the real decision lives.

## Meaning never rides on a glyph

Setting a console's encoding changes what can be transmitted, never what can be drawn: the terminal still needs a font containing the character, and nothing exposes font coverage. On a console with a raster font, `✓` and `✗` collapse into two identical missing-glyph boxes — "passed" and "failed" rendered the same, which is worse than two distinct wrong characters.

So load-bearing state — pass/fail, error, warning — travels in words and colour. A glyph may decorate it; a glyph may never be the only thing carrying it.

## Automation that feeds untrusted content to an LLM

Never build a workflow that automatically feeds untrusted content — PR titles, bodies, diffs, comments, branch contents — into an LLM that holds any write capability, however narrow.
Flag the design rather than iterating on it, and propose on-demand invocation instead: an `@claude` mention, or a maintainer-run CLI review.

Two independent reasons:

1. **Prompt injection is structural, not patchable.** The combination is a prompt-injection target by construction. Tool lockdown, permission scoping, and input sanitization all patch symptoms: no filter catches every jailbreak, and any write capability is leverage for whoever gets one through.
2. **Generic LLM review is low-signal.** Auto-triggered review actions typically don't load `CLAUDE.md` or `.claude/rules/`, so they emit style-guide-shaped feedback with no project specificity. We have lived this with Copilot review — "every time the same ado about nothing."

If such a workflow already exists and is broken, weigh removing it against fixing it instead of defaulting to a fix.
The reasoning extends past PR review: any auto-trigger that pipes external content into an LLM holding write tools deserves the same flag.
