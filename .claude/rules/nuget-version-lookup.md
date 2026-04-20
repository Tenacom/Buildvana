# NuGet version lookup procedure

Use this procedure to determine the target version for a NuGet package, applying the rule:
- latest stable if already using a stable version;
- latest pre-release of the same major.minor if the current version is a pre-release (if one exists);
- otherwise fall back to latest stable.

## Step 1 — read package sources from nuget.config

Read `nuget.config` (XML). Extract all enabled `<add>` entries under `<packageSources>`.
Use v3 feeds (URL ends in /index.json); skip v2 if a v3 feed exists for the same source.

## Step 2 — resolve the registration base URL for each source

For each v3 source URL, WebFetch `{sourceUrl}` (the index.json).
Find the resource with type `RegistrationsBaseUrl/3.6.0`, or fall back to
`RegistrationsBaseUrl/3.0.0-beta`, or `RegistrationsBaseUrl`.
Extract its `@id` value — this is the `registrationsBaseUrl`.

## Step 3 — fetch the registration index for the package

WebFetch `{registrationsBaseUrl}{packageId-lowercase}/index.json`.

The response contains `items` (pages). Each page either:
- Contains `items` inline (small packages), or
- Has only a `@id` URL that must be fetched separately to get its `items`.

Each leaf item has a `catalogEntry` object with:
- `version` — the version string
- `listed` — must be `true` (skip unlisted packages)

Collect and deduplicate all listed versions across all pages and sources.

## Step 4 — determine the target version

IF `currentVersion` is a pre-release THEN BEGIN
  - Extract `currentMajor.currentMinor` from `currentVersion`.
  - Filter collected versions to pre-release versions whose `major.minor`
    matches `currentMajor.currentMinor`.
  - If any such versions exist → target = highest among them.
  - If none exist → target = highest stable version from the collected list.
END ELSE BEGIN
  - Target = highest stable version from the collected list.
END

## Notes

- Query all sources; take the overall highest qualifying version across feeds.
- If a source is unreachable, skip it and continue with remaining sources.
- NuGet package IDs are case-insensitive; always lowercase them in URLs.
