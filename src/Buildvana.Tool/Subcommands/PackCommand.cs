// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Tool.Build;
using Buildvana.Tool.Infrastructure.Execution;

namespace Buildvana.Tool.Subcommands;

[ImplementsCommand("pack", consumesAllArguments: true)]
[Description("Clean, restore, build all projects, run tests, and prepare build artifacts.")]
internal sealed class PackCommand(BuildPipeline pipeline) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        await pipeline.RunThroughAsync(BuildStep.Pack, cancellationToken: cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
