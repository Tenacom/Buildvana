// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Buildvana.Core.Json;

public sealed partial class JsonHelper
{
    // Collects the splices to apply to a document's bytes. A span cannot be a generic type argument, so the
    // shared rewriting plumbing takes its walker as this delegate rather than as a Func.
    private delegate List<JsonValueEdit> EditCollector(ReadOnlySpan<byte> jsonSpan, int offsetInFile);
}
