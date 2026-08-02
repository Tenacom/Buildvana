// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Versioning;
using Spectre.Console;

namespace Buildvana.Tool.Subcommands;

[ImplementsCommand("version show | version", defaultVerbosity: Verbosity.Minimal)]
[Description("Show current and published version information.")]
internal sealed class VersionShowCommand(VersionService version, GitService git, IAnsiConsole console) : IBvCommand
{
    public Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        // The report is the command's deliverable, not narration: it goes to the console unconditionally,
        // regardless of verbosity, like the query commands of git, npm, or dotnet.
        var branch = git.CurrentBranch;
        console.WriteLine($"Current version:       {version.CurrentStr}");
        console.WriteLine($"Latest version:        {version.Latest?.ToString() ?? "(none)"}");
        console.WriteLine($"Latest stable version: {version.LatestStable?.ToString() ?? "(none)"}");
        console.WriteLine($"Public release:        {(version.IsPublicRelease ? "yes" : "no")}");
        console.WriteLine($"Prerelease:            {(version.IsPrerelease ? "yes" : "no")}");
        console.WriteLine($"Current branch:        {(branch.Length == 0 ? "(detached HEAD)" : branch)}");
        return Task.FromResult(0);
    }
}
