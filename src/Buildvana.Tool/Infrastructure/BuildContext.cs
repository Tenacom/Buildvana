// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.PublicApiFiles;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Versioning;
using Cake.Core;
using Cake.Frosting;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Infrastructure;

public sealed class BuildContext : FrostingContext
{
    private readonly IServiceProvider _services;

    public BuildContext(ICakeContext context)
        : base(context)
    {
        Guard.IsNotNull(context);
        _services = new ServiceCollection()
            .AddSingleton(context)
            .AddSingleton<GitService>()
            .AddSingleton<PublicApiFilesService>()
            .AddSingleton(ServerAdapter.Create)
            .AddSingleton<VersionService>()
            .AddSingleton<ChangelogService>()
            .AddSingleton<DocFxService>()
            .AddSingleton<DotNetService>()
            .AddSingleton<OptionsService>()
            .AddSingleton<PathsService>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    /// <summary>
    /// Gets a service from the global service locator,
    /// failing if the requested service cannot be provided.
    /// </summary>
    /// <typeparam name="TService">The type of the requested service.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TService GetService<TService>()
        where TService : notnull
        => _services.GetRequiredService<TService>();
}
