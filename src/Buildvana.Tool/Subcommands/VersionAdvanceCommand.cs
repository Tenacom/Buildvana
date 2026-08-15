// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Versioning;
using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services.Versioning;

namespace Buildvana.Tool.Subcommands;

[ImplementsCommand("version advance", settingsType: typeof(VersionAdvanceSettings))]
[Description("Advance the version spec in the VERSION file, leaving the change uncommitted for review.")]
internal sealed class VersionAdvanceCommand(
    VersionAdvanceSettings settings,
    BuildvanaConfig config,
    VersionService version,
    VersioningSettings versioningSettings,
    IHomeDirectoryProvider home,
    IReporter reporter) : IBvCommand
{
    public Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var requestedChange = settings.ResolveChange();
        var change = settings.Force
            ? requestedChange
            : version.ComputeVersionSpecChange(requestedChange, config.Release.CheckPublicApi);

        var versionFile = VersionFile.Load(home);
        var previousSpec = versionFile.Spec;
        if (versionFile.ApplyChange(change))
        {
            versionFile.Save(versioningSettings.PrereleaseTag);
            reporter.Notice($"Version spec changed from {previousSpec} to {versionFile.Spec}.");
            reporter.Notice($"Review and commit the modified {VersionFile.FileName} file.");
        }
        else
        {
            reporter.Notice("Version spec not changed.");
        }

        return Task.FromResult(0);
    }
}
