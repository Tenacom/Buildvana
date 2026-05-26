// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Tool.Infrastructure.Execution;

namespace Buildvana.Tool.Commands;

[ImplementsCommand("build", consumesAllArguments: true)]
[Description("Clean, restore, and build all projects.")]
internal sealed class BuildCommand(IServiceProvider services) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        await BuildSteps.CleanAsync(services).ConfigureAwait(false);
        await BuildSteps.RestoreAsync(services).ConfigureAwait(false);
        await BuildSteps.BuildAsync(services).ConfigureAwait(false);
        return 0;
    }
}
