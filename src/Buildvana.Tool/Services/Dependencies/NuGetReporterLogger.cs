// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Buildvana.Core.ConsoleOutput;
using CommunityToolkit.Diagnostics;
using NuGet.Common;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Says through <c>bv</c>'s reporter what the NuGet client libraries have to say.
/// </summary>
/// <remarks>
/// <para>NuGet's levels are mapped onto <c>bv</c>'s ladder rather than onto their namesakes. What a library
/// calls minimal is still narration of somebody else's work, so it lands at detail verbosity, leaving the
/// default output to the command's own report.</para>
/// </remarks>
internal sealed class NuGetReporterLogger(IReporter reporter) : ILogger
{
    /// <inheritdoc/>
    public void LogDebug(string data) => Log(LogLevel.Debug, data);

    /// <inheritdoc/>
    public void LogVerbose(string data) => Log(LogLevel.Verbose, data);

    /// <inheritdoc/>
    public void LogInformation(string data) => Log(LogLevel.Information, data);

    /// <inheritdoc/>
    public void LogMinimal(string data) => Log(LogLevel.Minimal, data);

    /// <inheritdoc/>
    public void LogWarning(string data) => Log(LogLevel.Warning, data);

    /// <inheritdoc/>
    public void LogError(string data) => Log(LogLevel.Error, data);

    /// <inheritdoc/>
    public void LogInformationSummary(string data) => Log(LogLevel.Information, data);

    /// <inheritdoc/>
    public void Log(LogLevel level, string data) => reporter.Report(LevelOf(level), data);

    /// <inheritdoc/>
    public void Log(ILogMessage message)
    {
        Guard.IsNotNull(message);
        Log(message.Level, message.Message);
    }

    /// <inheritdoc/>
    public Task LogAsync(LogLevel level, string data)
    {
        Log(level, data);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task LogAsync(ILogMessage message)
    {
        Log(message);
        return Task.CompletedTask;
    }

    private static MessageLevel LevelOf(LogLevel level)
        => level switch
        {
            LogLevel.Error => MessageLevel.Error,
            LogLevel.Warning => MessageLevel.Warning,
            LogLevel.Debug => MessageLevel.Trace,
            _ => MessageLevel.Detail,
        };
}
