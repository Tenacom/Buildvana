// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services;
using Spectre.Console;

namespace Buildvana.Tool.Subcommands;

[ImplementsCommand("update", settingsType: typeof(UpdateSettings))]
[Description("Update the repository's Buildvana pins (tool manifest, global.json, configuration schema reference) to this bv's version.")]
internal sealed class UpdateCommand(SelfVersionService selfVersion, UpdateSettings settings, IAnsiConsole console) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var summary = await selfVersion.UpdateRepositoryAsync(settings.Force, cancellationToken).ConfigureAwait(false);

        // The summary is the command's deliverable, not narration: it goes to the console unconditionally,
        // regardless of verbosity, like the report of `bv version show`.
        console.WriteLine(summary.ToolManifestLine);
        console.WriteLine(summary.GlobalJsonLine);
        if (summary.ConfigFileLine is not null)
        {
            console.WriteLine(summary.ConfigFileLine);
        }

        return 0;
    }
}
