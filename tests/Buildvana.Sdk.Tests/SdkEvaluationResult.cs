// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

internal sealed record SdkEvaluationResult(string HomeDirectory, IReadOnlyList<SdkEvaluationError> Errors);
