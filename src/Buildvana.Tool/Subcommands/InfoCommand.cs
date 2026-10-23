// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Hosts;
using Buildvana.Tool.Services.Platforms;
using Buildvana.Tool.Services.Release;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Buildvana.Tool.Subcommands;

[ImplementsCommand("info")]
[Description("Show the CI platform, the Git host, and what a release would use.")]
internal sealed class InfoCommand(
    IServiceProvider services,
    BuildvanaConfig config,
    CIPlatform platform,
    IAnsiConsole console) : IBvCommand
{
    private const string None = "(none)";

    public Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        // The report is the command's deliverable, as for `bv version show`: it goes to the console at every
        // verbosity. A line that cannot be resolved states the reason, and the command still succeeds, because
        // the reason is what the user runs the command to learn.
        var remote = TryGet(() => services.GetRequiredService<ReleaseRemoteResolver>().Resolve(), out var remoteError);
        var host = TryGet(services.GetRequiredService<GitHost>, out var hostError);
        var git = TryGet(services.GetRequiredService<IGitService>, out _);
        WriteLine("CI platform:", platform.Name);
        WriteLine("Server URL:", platform.ServerUrl?.ToString() ?? None);
        WriteLine("Bot identity:", platform.BotIdentity is { } bot ? Describe(bot) : None);
        WriteLine("ContinuousIntegrationBuild:", platform.IsCI ? "true" : "false");
        WriteLine("Repository URL:", config.Repository.Url is { Length: > 0 } url ? url : None);
        WriteLine("Release remote:", remote ?? $"{None} {remoteError}");
        WriteLine("Git host:", host is not null ? DescribeHost(host) : $"{None} {hostError}");
        WriteLine("Token variable:", host is not null ? DescribeToken(host) : None);
        WriteLine("Release identity:", DescribeIdentity(git));
        return Task.FromResult(0);
    }

    private static T? TryGet<T>(Func<T> get, out string? error)
        where T : class
    {
        try
        {
            error = null;
            return get();
        }
        catch (BuildFailedException e)
        {
            error = e.Message;
            return null;
        }
    }

    private static string Describe(GitIdentity identity) => $"{identity.Name} <{identity.Email}>";

    private static string DescribeToken(GitHost host)
        => host.TokenEnv is { } name ? $"{name} ({(host.GetToken() is null ? "not set" : "set")})" : None;

    // The writer of the console, not the console itself: the console wraps text at its width, 80 columns when
    // the output is redirected, and a wrapped line is no longer one line per item.
    private void WriteLine(string label, string value) => console.Profile.Out.Writer.WriteLine($"{label,-28}{value}");

    private string DescribeHost(GitHost host)
    {
        if (host.Kind is not null)
        {
            return $"{host.Name} ({host.HostName})";
        }

        return config.Release.AllowUnknownHost
            ? $"{host.Name}, allowed by release.allowUnknownHost"
            : $"{host.Name}, refused by bv release unless release.allowUnknownHost is true";
    }

    private string DescribeIdentity(IGitService? git)
        => ReleaseCommitterIdentity.Resolve(config.Git, platform, git?.CommitterIdentity) is (var identity, var source)
            ? $"{Describe(identity)}, from {source}"
            : None;
}
