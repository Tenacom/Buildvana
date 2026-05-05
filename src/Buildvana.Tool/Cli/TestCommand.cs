// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using Spectre.Console.Cli;

namespace Buildvana.Tool.Cli;

[Description("Build all projects and run tests.")]
internal sealed class TestCommand(IServiceProvider services) : AsyncCommand<BuildSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, BuildSettings settings, CancellationToken cancellationToken)
    {
        Guard.IsNotNull(settings);
        SettingsApplier.Apply(settings, services);
        await BuildSteps.CleanAsync(services).ConfigureAwait(false);
        await BuildSteps.RestoreAsync(services).ConfigureAwait(false);
        await BuildSteps.BuildAsync(services).ConfigureAwait(false);
        await BuildSteps.TestAsync(services).ConfigureAwait(false);
        return 0;
    }
}
