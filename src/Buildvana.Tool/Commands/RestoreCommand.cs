// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Tool.Infrastructure.Execution;
using CommunityToolkit.Diagnostics;
using Spectre.Console.Cli;

namespace Buildvana.Tool.Commands;

[ImplementsCommand("restore", consumesAllArguments: true)]
[Description("Clean and restore dependencies.")]
internal sealed class RestoreCommand(IServiceProvider services) : AsyncCommand<BaseSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
    {
        Guard.IsNotNull(settings);
        await BuildSteps.CleanAsync(services).ConfigureAwait(false);
        await BuildSteps.RestoreAsync(services).ConfigureAwait(false);
        return 0;
    }
}
