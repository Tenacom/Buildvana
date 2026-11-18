# Project design principles

This file is specific to this repository. Rewrite it when `.claude` is copied to another repository. `design-principles.md` holds the design principles that hold in every repository. This file holds the ones about Buildvana alone.

## Portability of what we ship

A tool we ship must behave the same on any CI platform and on a developer laptop. When a tool needs authentication, identity, secrets, or any other runtime input, take it from a mechanism the tool controls end to end. That mechanism is the tool's own configuration file, with explicit environment-variable references inside config values. Do not take the input from whatever the host CI set up in the workspace.

"The runner sets it up for us" is a defect, not a feature. When a proposed fix works on one CI platform only, say so. Shelling out to native `git` to inherit the installed credential helpers has the same defect, so say so there too.

This rule is about user-visible portability. A platform adapter that exists by design is fine. A dependency on the runner environment in the core flow is not. This rule applies the first limit of "Scope: our workflows, not everyone's" in `design-principles.md`. "Tool portability" below describes the incident that produced this rule.

## Tool portability

`bv` must behave the same on GitHub Actions, GitLab CI, a self-hosted runner, and a developer laptop. "Portability of what we ship" above states the general rule. This section records the incident behind it.

`bv release` failed on a GitHub runner. The `Network.Push(...)` call of `LibGit2Sharp` had no `CredentialsProvider`, so it relied on whatever credentials `actions/checkout` left in `.git/config`. That mechanism has no GitLab CI equivalent, and it changed between `actions/checkout` v3 and v5. It is also invisible from our own code, so the failure looked mysterious instead of like the missing-configuration bug it was.

The lesson covers anything `bv` needs at runtime: identity, tokens, feed URLs. The configuration file is the one override mechanism. Do not add a second one that would compete with it.

## Conformance with the toolchain we belong to

Buildvana is a component of a .NET toolchain. Where that toolchain has settled a question of behavior, we settle it the same way, instead of deciding it on the merits. Examples:

- A console configured to suit `dotnet build` must suit `bv build` identically.
- The verbosity a user passes to `bv` reaches the `dotnet` invocations underneath.
- An existing convention is honored by the rules of whoever defined it. `NO_COLOR` follows its own rules, and `DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING` follows the .NET CLI's, down to the details where the two disagree.

Borrowed conventions are not harmonized with each other. Each belongs to whoever defined it, and making them consistent would mean obeying neither.

This rule outranks "we would have designed it better". It differs from "Scope: our workflows, not everyone's" in `design-principles.md`: that one governs which features to build, this one governs matching established behavior in the ones we build.

Conformance is about observable behavior and the switches that govern it, never about source. "Code from other projects" in `design-principles.md` holds the rules about source. Borrow a constant or a rule where it states a fact worth having. The Windows build below which a console host cannot be trusted at codepage 65001 is one such fact. When you reimplement a behavior, prefer a mechanism that needs no maintenance, such as catching what an unsupported platform throws, over one that lists every known platform.

Deviate only where the convention's premise does not hold for us, and say so at the point of deviation. Example: `bv` does not copy the CLI's non-English-UI-culture gate on the console encoding switch. That gate exists to make localized CLI output render, and Buildvana is single-culture English. The reason for the switch itself is language-independent, so the switch stays.

Before answering a design question from first principles, check whether `dotnet`, MSBuild, or NuGet has answered it. Read their source instead of recalling it. The decision is usually in the guard clauses, and memory drops those.
