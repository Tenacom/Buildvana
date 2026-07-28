// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// An <see cref="AnalyzerConfigOptions"/> backed by a dictionary.
/// </summary>
internal sealed class TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> options) : AnalyzerConfigOptions
{
    public static TestAnalyzerConfigOptions Empty { get; } = new(new Dictionary<string, string>());

    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        if (options.TryGetValue(key, out var found))
        {
            value = found;
            return true;
        }

        value = null;
        return false;
    }
}
