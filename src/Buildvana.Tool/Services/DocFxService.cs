// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Versioning;
using Cake.Core.IO;
using CommunityToolkit.Diagnostics;

using SysFile = System.IO.File;

namespace Buildvana.Tool.Services;

/// <summary>
/// Implements DocFX operations.
/// </summary>
public sealed class DocFxService
{
    private readonly ServerAdapter _server;
    private readonly VersionService _version;
    private readonly PathsService _paths;
    private readonly FilePath _configPath;

    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocFxService"/> class.
    /// </summary>
    /// <param name="host">The build host.</param>
    /// <param name="server">The server adapter.</param>
    /// <param name="version">The version management service.</param>
    /// <param name="paths">The service providing path information.</param>
    public DocFxService(
        IBuildHost host,
        ServerAdapter server,
        VersionService version,
        PathsService paths)
    {
        Guard.IsNotNull(host);
        Guard.IsNotNull(server);
        Guard.IsNotNull(version);
        Guard.IsNotNull(paths);

        _server = server;
        _version = version;
        _paths = paths;

        _configPath = _paths.Docs.CombineWithFilePath("docfx.json");
        IsEnabled = SysFile.Exists(_configPath.FullPath);
        if (!IsEnabled)
        {
            host.LogInformation($"{_configPath} not found: DocFX operations will be skipped.");
        }
    }

    public bool IsEnabled { get; }

    /// <summary>
    /// Asynchronously generates a documentation web site according to <c>docfx.json</c> settings.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task GenerateSiteAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        Initialize();

        await Docfx.Docset.Build(_configPath.FullPath).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously generates PDF documentation files according to <c>docfx.json</c> settings.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task GeneratePdfsAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        Initialize();

        await Docfx.Docset.Pdf(_configPath.FullPath).ConfigureAwait(false);
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        var globalMetadata = new
        {
            RepoOwner = _server.RepositoryOwner,
            RepoName = _server.RepositoryName,
            RepoUrl = _server.RepositoryUrl,
            RepoVersion = _version.CurrentStr,
        };

#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances - This one is used just once.
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
#pragma warning restore

        var jsonPath = _paths.Docs.CombineWithFilePath("globalMetadata.json");
        using var stream = SysFile.Create(jsonPath.FullPath);
        JsonSerializer.Serialize(stream, globalMetadata, options);
    }
}
