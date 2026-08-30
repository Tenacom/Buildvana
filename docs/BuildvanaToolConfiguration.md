# The Buildvana configuration file

TODO: document `buildvana.json` / `buildvana.jsonc` — discovery, home-directory marking, validation, and every setting.

Until then, three files cover the ground between them.

- [`schemas/buildvana.schema.json`](../schemas/buildvana.schema.json) is the JSON schema, generated from the typed model. It names every setting, with its description, its built-in default value, and an example where one helps. An editor reads it to validate the file and to complete it.
- [`buildvana.example.jsonc`](../buildvana.example.jsonc) is a worked example, generated from that schema. Every setting appears, introduced by its description and carrying an example or default value. Nothing reads the file: copy out of it what you need.
- [`buildvana.jsonc`](../buildvana.jsonc) is this repository's own configuration. It states what Buildvana sets for itself, and records in one line of prose each omission that is a decision.
