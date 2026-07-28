// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// An <see cref="AnalyzerConfigOptionsProvider"/> backed by dictionaries,
/// providing global options and per-additional-file options.
/// </summary>
internal sealed class TestAnalyzerConfigOptionsProvider(
    IReadOnlyDictionary<string, string> globalOptions,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> fileOptions) : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions => new TestAnalyzerConfigOptions(globalOptions);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestAnalyzerConfigOptions.Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        => fileOptions.TryGetValue(textFile.Path, out var options)
            ? new TestAnalyzerConfigOptions(options)
            : TestAnalyzerConfigOptions.Empty;
}
