// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Subcommands;

internal sealed class SettingsHelpReflectionTests
{
    [Test]
    public async Task GlobalSettings_ExposesOptionsInHelpOrder()
    {
        var names = OptionLongNames(typeof(GlobalSettings));
        await Assert.That(names).IsEqualTo("--verbosity,--color,--no-color,--nologo,--version");
    }

    [Test]
    public async Task ReleaseSettings_ExposesOptionsInHelpOrder()
    {
        var names = OptionLongNames(typeof(ReleaseSettings));
        await Assert.That(names).IsEqualTo("--configuration,--bump,--check-public-api,--dogfood");
    }

    private static string OptionLongNames(Type type)
    {
        var longNames = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static p => p.GetCustomAttribute<BvOptionAttribute>())
            .Where(static a => a is not null)
            .Select(static a => a!.LongNames[0]);
        return string.Join(",", longNames);
    }
}
