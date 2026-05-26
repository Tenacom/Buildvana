// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Tool.Infrastructure.Execution;

namespace Buildvana.Tool.Subcommands;

[ImplementsCommand("pack", consumesAllArguments: true)]
[Description("Clean, restore, build all projects, run tests, and prepare build artifacts.")]
internal sealed class PackCommand(IServiceProvider services) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        await BuildSteps.CleanAsync(services).ConfigureAwait(false);
        await BuildSteps.RestoreAsync(services).ConfigureAwait(false);
        await BuildSteps.BuildAsync(services).ConfigureAwait(false);
        await BuildSteps.TestAsync(services).ConfigureAwait(false);
        await BuildSteps.PackAsync(services).ConfigureAwait(false);
        return 0;
    }
}
