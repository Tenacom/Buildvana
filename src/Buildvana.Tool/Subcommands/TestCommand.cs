// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Tool.Build;
using Buildvana.Tool.Infrastructure.Execution;

namespace Buildvana.Tool.Subcommands;

[ImplementsCommand("test", consumesAllArguments: true)]
[Description("Build all projects and run tests.")]
internal sealed class TestCommand(BuildPipeline pipeline) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        await pipeline.RunThroughAsync(BuildStep.Test).ConfigureAwait(false);
        return 0;
    }
}
