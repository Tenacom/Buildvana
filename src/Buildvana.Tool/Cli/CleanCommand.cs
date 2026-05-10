// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using Spectre.Console.Cli;

namespace Buildvana.Tool.Cli;

[Description("Remove all build artifacts, intermediate output, and temporary files. Like 'dotnet clean', but more aggressive.")]
internal sealed class CleanCommand(IServiceProvider services) : AsyncCommand<BaseSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
    {
        Guard.IsNotNull(settings);
        settings.Apply(services);
        await BuildSteps.CleanAsync(services).ConfigureAwait(false);
        return 0;
    }
}
