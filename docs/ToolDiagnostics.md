# Diagnostics issued by `bv`

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [Main program (1000-1099)](#main-program-1000-1099)
- [Configuration (1100-1199)](#configuration-1100-1199)

## Overview

All diagnostics issued by the `bv` CLI tool have a `BV` prefix. All numbers start from 1000, so there are no leading zeros.

Each part of the program is assigned a contiguous range of 100 diagnostics, as listed below. The first range is reserved for the main program.

## Main program (1000-1099)

There are no associated diagnostics.

## Configuration (1100-1199)

| Code   | Severity | Message                                               | Description                                                                                                                                  |
| ------ | :------: | ----------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| BV1100 |  Error   | _(the JSON parser's reason)_                          | The configuration file could not be parsed as JSON. The message carries the parser's reason; the location points at the offending character. |
| BV1101 |  Error   | Expected _(type)_, but found _(type)_.                | A value has a type the schema does not allow at that location (for example, a number where a string is required, or an explicit `null`).     |
| BV1102 |  Error   | _(value)_ is not one of the allowed values: _(list)_. | A value is not among those the schema permits at that location (for example, an unknown enumeration value).                                  |
| BV1103 |  Error   | Unknown property '_(name)_'.                          | The configuration file contains a property the schema does not define, or a dictionary key outside the allowed set.                          |
| BV1104 |  Error   | Missing required property '_(name)_'.                 | A property the schema marks as required is absent.                                                                                           |
| BV1105 |  Error   | No value is allowed here.                             | A value appears at a location where the schema permits none.                                                                                 |
