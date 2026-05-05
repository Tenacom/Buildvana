// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Versioning;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Buildvana.Tool.Services;

/// <summary>
/// Implements DocFX operations.
/// </summary>
public sealed class DocFxService
{
    private readonly ServerAdapter _server;
    private readonly VersionService _version;
    private readonly string _configPath;

    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocFxService"/> class.
    /// </summary>
    public DocFxService(
        ILogger<DocFxService> logger,
        ServerAdapter server,
        VersionService version)
    {
        Guard.IsNotNull(logger);
        Guard.IsNotNull(server);
        Guard.IsNotNull(version);

        _server = server;
        _version = version;

        _configPath = Path.Combine(CommonPaths.Docs, "docfx.json");
        IsEnabled = File.Exists(_configPath);
        if (!IsEnabled)
        {
            logger.LogInformation("{ConfigPath} not found: DocFX operations will be skipped.", _configPath);
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

        await Docfx.Docset.Build(_configPath).ConfigureAwait(false);
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

        await Docfx.Docset.Pdf(_configPath).ConfigureAwait(false);
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

        var jsonPath = Path.Combine(CommonPaths.Docs, "globalMetadata.json");
        using var stream = File.Create(jsonPath);
        JsonSerializer.Serialize(stream, globalMetadata, options);
    }
}
