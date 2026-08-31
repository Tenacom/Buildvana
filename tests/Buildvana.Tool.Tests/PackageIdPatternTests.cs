// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Dependencies;

internal sealed class PackageIdPatternTests
{
    [Test]
    [Arguments("Serilog", "Serilog")]
    [Arguments("serilog", "Serilog")] // package ids are case-insensitive
    [Arguments("SERILOG", "Serilog")]
    [Arguments("*", "Serilog")]
    [Arguments("*", "")]
    [Arguments("Microsoft.CodeAnalysis.*", "Microsoft.CodeAnalysis.CSharp")]
    [Arguments("*.Analyzers", "StyleCop.Analyzers")]
    [Arguments("Microsoft.*.Analyzers", "Microsoft.CodeAnalysis.Analyzers")]
    [Arguments("Serilog*", "Serilog")] // a wildcard matches no characters too
    [Arguments("**Serilog**", "Serilog")]
    [Arguments("a*b*c", "aXXbYYc")]
    public async Task Matches_WhenTheWholeIdMatches_IsTrue(string pattern, string id)
    {
        await Assert.That(PackageIdPattern.Matches(pattern, id)).IsTrue();
    }

    [Test]
    [Arguments("Serilog", "Serilog.Sinks.Console")] // the whole id must match, not a prefix of it
    [Arguments("Serilog.Sinks.Console", "Serilog")]
    [Arguments("Microsoft.CodeAnalysis.*", "Microsoft.Build.Traversal")]
    [Arguments("*.Analyzers", "StyleCop.Analyzers.Extra")]
    [Arguments("", "Serilog")]
    [Arguments("a*b*c", "aXXbYY")]
    public async Task Matches_WhenTheIdDoesNot_IsFalse(string pattern, string id)
    {
        await Assert.That(PackageIdPattern.Matches(pattern, id)).IsFalse();
    }

    // Nothing but `*` is a wildcard: a pattern meant for a shell matches nothing here, and says so instead
    // of matching something unexpected.
    [Test]
    [Arguments("Seril?g", "Serilog")]
    [Arguments("Serilog.[Ss]inks", "Serilog.Sinks")]
    public async Task Matches_TreatsEveryOtherCharacterAsItself(string pattern, string id)
    {
        await Assert.That(PackageIdPattern.Matches(pattern, id)).IsFalse();
    }
}
