// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buildvana.Core;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitLab;

/// <summary>
/// ServerRelease implementation for GitLab.
/// </summary>
internal sealed class GitLabServerRelease : ServerRelease
{
    private readonly GitLabServerAdapter _server;
    private readonly IBuildHost _host;

    private GitLabServerRelease(GitLabServerAdapter server, IServiceProvider services)
        : base(services)
    {
        Guard.IsNotNull(server);
        Guard.IsNotNull(services);

        _server = server;
        _host = services.GetRequiredService<IBuildHost>();
    }

    public static Task<GitLabServerRelease> CreateAsync(GitLabServerAdapter server, IServiceProvider services)
    {
        Guard.IsNotNull(server);
        Guard.IsNotNull(services);

        return Task.FromResult(new GitLabServerRelease(server, services));
    }

    protected override Task DoPublishAsync(IReadOnlyList<AssetData> assets) => _host.FailOnUnsupportedMethod<Task>();

    protected override Task UndoPublishAsync() => _host.FailOnUnsupportedMethod<Task>();
}
