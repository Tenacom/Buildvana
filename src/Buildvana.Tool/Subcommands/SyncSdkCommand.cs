// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services;

namespace Buildvana.Tool.Subcommands;

[ImplementsCommand("sync-sdk")]
[Description("Align the pinned Buildvana SDK version with this bv's version, updating whichever is older.")]
internal sealed class SyncSdkCommand(SelfVersionService selfVersion) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        await selfVersion.SyncSdkAsync(cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
