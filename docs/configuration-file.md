# The Buildvana configuration file

`buildvana.jsonc` holds the settings of a repository that uses Buildvana.
`bv` and Buildvana SDK read it from the home directory.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Discovery](#discovery)
- [Settings](#settings)
- [Related files](#related-files)

---

## Discovery

The file is named `buildvana.jsonc` or `buildvana.json`.
Comments and trailing commas are accepted under either name.
A home directory that holds both is an error, [BVSDK1005](sdk-diagnostics.md#buildvana-sdk-core-1000-1049) in Buildvana SDK.

The file is a home-directory marker.
Home-directory discovery stops at the nearest directory that holds one, and [Location of the home directory](directory-structure.md#location-of-the-home-directory) lists the other markers.
An empty JSON object, `{}`, is valid content, and marks a directory without configuring anything.

## Settings

The `dependencies` section is documented in [Dependency management](tool-commands/dependencies.md), together with the command it configures.
The files below describe every other setting.

## Related files

- [`schemas/buildvana.schema.json`](../schemas/buildvana.schema.json) is the JSON schema, generated from the typed model.
  It names every setting, with its description, its built-in default value, and an example where one helps.
  An editor reads it to validate the file and to complete it.
- [`buildvana.example.jsonc`](../buildvana.example.jsonc) is a worked example, generated from that schema.
  Every setting appears, introduced by its description and carrying an example or default value.
  Nothing reads the file: copy out of it what you need.
- [`buildvana.jsonc`](../buildvana.jsonc) is this repository's own configuration, as the pinned version of `bv` reads it.
  It states what Buildvana sets for itself, and records in one line of prose each omission that is a decision.
- [`buildvana.next.jsonc`](../buildvana.next.jsonc) is the same configuration, as the _next_ version of `bv` will read it, so it may state a setting the pinned version rejects.
  The `release/post-release` hook copies it over `buildvana.jsonc`, in the same commit that moves the tool pin.
