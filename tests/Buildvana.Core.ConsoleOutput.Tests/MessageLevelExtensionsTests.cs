// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;

internal sealed class MessageLevelExtensionsTests
{
    [Test]
    [Arguments(MessageLevel.Error, Verbosity.Quiet)]
    [Arguments(MessageLevel.Warning, Verbosity.Minimal)]
    [Arguments(MessageLevel.Notice, Verbosity.Minimal)]
    [Arguments(MessageLevel.Info, Verbosity.Normal)]
    [Arguments(MessageLevel.Detail, Verbosity.Detailed)]
    [Arguments(MessageLevel.Trace, Verbosity.Diagnostic)]
    public async Task MinimumVerbosity_KnownLevel_ReturnsThreshold(MessageLevel level, Verbosity expected)
    {
        await Assert.That(level.MinimumVerbosity()).IsEqualTo(expected);
    }

    [Test]
    public async Task MinimumVerbosity_CoversEveryLevel()
    {
        // Guards the switch against a level added without a threshold: the arm would be missing, not wrong,
        // and no per-level test above would notice.
        foreach (var level in Enum.GetValues<MessageLevel>())
        {
            await Assert.That(() => level.MinimumVerbosity()).ThrowsNothing();
        }
    }

    [Test]
    [Arguments(-1)]
    [Arguments(42)]
    public async Task MinimumVerbosity_UnknownLevel_Throws(int level)
    {
        var exception = await Assert.That(() => ((MessageLevel)level).MinimumVerbosity()).Throws<ArgumentOutOfRangeException>();
        await Assert.That(exception?.ParamName).IsEqualTo("level");
    }
}
