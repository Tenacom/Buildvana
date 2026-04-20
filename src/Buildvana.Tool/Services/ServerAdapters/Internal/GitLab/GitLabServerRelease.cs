// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buildvana.Tool.Utilities;
using Cake.Core;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitLab;

/// <summary>
/// ServerRelease implementation for GitLab.
/// </summary>
internal sealed class GitLabServerRelease : ServerRelease
{
    private readonly GitLabServerAdapter _server;
    private readonly ICakeContext _context;

    private GitLabServerRelease(GitLabServerAdapter server, IServiceProvider services)
        : base(services)
    {
        Guard.IsNotNull(server);
        Guard.IsNotNull(services);

        _server = server;
        _context = services.GetRequiredService<ICakeContext>();
    }

    public static Task<GitLabServerRelease> CreateAsync(GitLabServerAdapter server, IServiceProvider services)
    {
        Guard.IsNotNull(server);
        Guard.IsNotNull(services);

        return Task.FromResult(new GitLabServerRelease(server, services));
    }

    protected override Task DoPublishAsync(IReadOnlyList<AssetData> assets) => _context.FailOnUnsupportedMethod<Task>();

    protected override Task UndoPublishAsync() => _context.FailOnUnsupportedMethod<Task>();
}
