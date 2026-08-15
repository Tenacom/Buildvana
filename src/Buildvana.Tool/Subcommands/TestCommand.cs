// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Runtime;
using Buildvana.Tool.Build;
using Buildvana.Tool.Infrastructure.Execution;

namespace Buildvana.Tool.Subcommands;

[ImplementsCommand("test", consumesAllArguments: true, usesSdk: true)]
[Description("Clean, restore, build all projects, and run tests.")]
internal sealed class TestCommand(BuildPipeline pipeline, BuildvanaConfig config) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        await pipeline.RunThroughAsync(BuildStep.Test, config.DotNet.Configuration, cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
