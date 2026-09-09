// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

// ProjectType holds the five BV_Is*Project properties by name, as evaluated.
internal sealed record SdkEvaluationResult(
    string HomeDirectory,
    IReadOnlyList<SdkEvaluationError> Errors,
    IReadOnlyDictionary<string, string> ProjectType);
