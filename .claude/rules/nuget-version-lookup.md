# NuGet version lookup procedure

Use this procedure to determine the target version of a NuGet package. The rule:

- When the current version is stable, the target is the latest stable.
- When the current version is a prerelease, the target is the latest prerelease of the same `major.minor`, when one exists.
- Otherwise, the target is the latest stable.

## Step 1: read package sources from nuget.config

Read `nuget.config`. Extract every enabled `<add>` entry under `<packageSources>`. Use v3 feeds, whose URL ends in `/index.json`. Skip a v2 feed when a v3 feed exists for the same source.

## Step 2: resolve the registration base URL for each source

For each v3 source URL, WebFetch `{sourceUrl}`, the index.json. Find the resource of type `RegistrationsBaseUrl/3.6.0`, or fall back to `RegistrationsBaseUrl/3.0.0-beta`, or to `RegistrationsBaseUrl`. Its `@id` value is the `registrationsBaseUrl`.

## Step 3: fetch the registration index for the package

WebFetch `{registrationsBaseUrl}{packageId-lowercase}/index.json`.

The response contains `items`, the pages. A page either contains its `items` inline, as for a small package, or has only a `@id` URL. Fetch that URL to get the page's `items`.

Each leaf item has a `catalogEntry` object with:

- `version`: the version string
- `listed`: skip the item unless it is `true`

Collect and deduplicate the listed versions across all pages and sources.

## Step 4: determine the target version

- When `currentVersion` is a prerelease:
  - Extract `currentMajor.currentMinor` from `currentVersion`.
  - Filter the collected versions to the prereleases whose `major.minor` matches.
  - When any remain, the target is the highest of them.
  - When none remain, the target is the highest stable version in the collected list.
- Otherwise, the target is the highest stable version in the collected list.

## Notes

- Query every source. Take the highest qualifying version across all of them.
- When a source is unreachable, skip it and continue with the rest.
- NuGet package ids are case-insensitive. Lowercase them in URLs.
