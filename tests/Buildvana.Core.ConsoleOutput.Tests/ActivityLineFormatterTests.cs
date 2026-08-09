// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Buildvana.Core.ConsoleOutput;

internal sealed class ActivityLineFormatterTests
{
    [Test]
    [Arguments(1, "[1] Doing stuff: starting...")]
    [Arguments(3, "[3] Doing stuff: starting...")]
    public async Task FormatStart_PrefixesDepthAndSuffixesTitle(int depth, string expected)
        => await Assert.That(ActivityLineFormatter.FormatStart(depth, "Doing stuff")).IsEqualTo(expected);

    [Test]
    public async Task FormatOutcome_WithoutOutcomeMessage_EndsAfterElapsedTime()
    {
        var line = ActivityLineFormatter.FormatOutcome(1, "Doing stuff", TimeSpan.FromSeconds(2.5), null);
        await Assert.That(line).IsEqualTo("[1] Doing stuff: done (2.5s)");
    }

    [Test]
    public async Task FormatOutcome_WithOutcomeMessage_AppendsItAfterASeparator()
    {
        var line = ActivityLineFormatter.FormatOutcome(2, "Doing stuff", TimeSpan.FromSeconds(2.5), "all good");
        await Assert.That(line).IsEqualTo("[2] Doing stuff: done (2.5s) - all good");
    }

    [Test]
    [Arguments(0.0, "0.0")]
    [Arguments(0.04, "0.0")]
    [Arguments(0.26, "0.3")]
    [Arguments(90.0, "90.0")]
    public async Task FormatOutcome_RendersElapsedAsTotalSecondsWithOneDecimal(double seconds, string expected)
    {
        var line = ActivityLineFormatter.FormatOutcome(1, "Doing stuff", TimeSpan.FromSeconds(seconds), null);
        await Assert.That(line).IsEqualTo($"[1] Doing stuff: done ({expected}s)");
    }

    [Test]
    public async Task FormatOutcome_UnderACommaDecimalCulture_StillUsesTheInvariantSeparator()
    {
        // No [NotInParallel]: the current culture is ambient to the executing flow, not process-global. Its setter
        // writes an AsyncLocal whose change handler maintains the thread-static the getter reads, so the swap is
        // confined to this test's execution context and no sibling test can observe it. The restore below is
        // hygiene for the remainder of this test, not what keeps the others clean.
        var previousCulture = CultureInfo.CurrentCulture;
        string line;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
            line = ActivityLineFormatter.FormatOutcome(1, "Doing stuff", TimeSpan.FromSeconds(2.5), null);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        await Assert.That(line).IsEqualTo("[1] Doing stuff: done (2.5s)");
    }
}
