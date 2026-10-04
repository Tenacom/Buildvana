// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Process;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services;

/// <summary>
/// Hands the rest of a <c>bv self-update</c> run to the target version, once the tool manifest pins it.
/// </summary>
/// <remarks>
/// <para>The target version knows its own configuration model and its own family pin rules, so it is the one
/// that updates <c>global.json</c>, the family pins, and the schema reference. The handed-off run is
/// <c>dotnet tool run bv -- &lt;global options&gt; --nologo self-update --to &lt;target&gt;</c>, plus
/// <c>--force</c> when this run had it, in the home directory, with inherited standard streams. Its target
/// equals its own version, so it runs in place and never hands off again. Only <c>--to</c> and
/// <c>--force</c> cross the handoff, so a target that predates the other options still accepts the command
/// line.</para>
/// <para><c>--nologo</c> is always passed: this run printed the logo already, and the line this class reports
/// names the version that takes over.</para>
/// <para>The previous bv pin travels in <see cref="FromEnvVar"/>, so that the handed-off run reports the
/// manifest move it did not make itself. A target that predates the variable ignores it.</para>
/// </remarks>
internal sealed class SelfUpdateHandoff
{
    /// <summary>
    /// The environment variable set on the handed-off run, carrying the bv version the tool manifest pinned
    /// before this run moved it. Its value is empty when the manifest had no bv entry.
    /// </summary>
    public const string FromEnvVar = "BV_SELF_UPDATE_FROM";

    private const string NologoOption = "--nologo";

    private readonly IReporter _reporter;
    private readonly IHomeDirectoryProvider _home;
    private readonly IProcessRunner _processRunner;
    private readonly IReadOnlyList<string> _globalOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelfUpdateHandoff"/> class.
    /// </summary>
    /// <param name="reporter">The reporter to log to.</param>
    /// <param name="home">The provider of the home directory, where the handed-off run runs.</param>
    /// <param name="processRunner">The process runner used to run the target version.</param>
    /// <param name="globalOptions">The global options of this run, as command-line tokens, to forward.</param>
    public SelfUpdateHandoff(
        IReporter reporter,
        IHomeDirectoryProvider home,
        IProcessRunner processRunner,
        IReadOnlyList<string> globalOptions)
    {
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(home);
        Guard.IsNotNull(processRunner);
        Guard.IsNotNull(globalOptions);
        _reporter = reporter;
        _home = home;
        _processRunner = processRunner;
        _globalOptions = globalOptions;
    }

    /// <summary>
    /// Runs <c>bv self-update</c> on the target version, which the tool manifest pins by now.
    /// </summary>
    /// <param name="target">The target version.</param>
    /// <param name="force">Whether this run allowed a downgrade.</param>
    /// <param name="previousPin">The bv version the manifest pinned before this run, or <see langword="null"/>
    /// when it had no bv entry.</param>
    /// <param name="cancellationToken">A token that, when signalled, kills the handed-off run.</param>
    /// <returns>The exit code of the handed-off run.</returns>
    /// <exception cref="BuildFailedException">The handed-off run could not be started.</exception>
    public Task<int> RunAsync(
        NuGetVersion target,
        bool force,
        NuGetVersion? previousPin,
        CancellationToken cancellationToken)
    {
        Guard.IsNotNull(target);
        var targetText = target.ToNormalizedString();
        _reporter.Notice($"Handing off to {ToolManifest.BvPackageId} {targetText}, now pinned in this repository's tool manifest.");

        var hasNologo = _globalOptions.Any(static token => string.Equals(token, NologoOption, StringComparison.OrdinalIgnoreCase));
        IEnumerable<string> globalOptions = hasNologo ? _globalOptions : [.. _globalOptions, NologoOption];
        IEnumerable<string> options = force ? ["--to", targetText, "--force"] : ["--to", targetText];
        return _processRunner.RunWithInheritedStdioAsync(
            DotNetMuxer.Path,
            ["tool", "run", ToolManifest.BvPackageId, "--", .. globalOptions, "self-update", .. options],
            environment: new Dictionary<string, string?> { [FromEnvVar] = previousPin?.ToNormalizedString() ?? string.Empty },
            workingDirectory: _home.HomeDirectory,
            cancellationToken: cancellationToken);
    }
}
