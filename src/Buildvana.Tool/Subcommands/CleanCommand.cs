// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Tool.Infrastructure.Execution;

namespace Buildvana.Tool.Subcommands;

[ImplementsCommand("clean")]
[Description("Remove all build artifacts, intermediate output, and temporary files. Like 'dotnet clean', but more aggressive.")]
internal sealed class CleanCommand(IServiceProvider services) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        await BuildSteps.CleanAsync(services).ConfigureAwait(false);
        return 0;
    }
}
