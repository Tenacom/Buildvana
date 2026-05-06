// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Git;

/// <summary>
/// Represents information about a Git fetch URL that may be needed to identify the kind of server and other information.
/// </summary>
public sealed class GitUrlInfo
{
    private GitUrlInfo(
        Uri url,
        string host,
        int port,
        IReadOnlyList<string> pathSegments)
    {
        Url = url;
        Host = host;
        Port = port;
        PathSegments = pathSegments;
    }

    public Uri Url { get; }

    public string Host { get; }

    public int Port { get; }

    public IReadOnlyList<string> PathSegments { get; }

    public bool UseDefaultPort => Port < 0;

    public static bool TryCreate(Uri url, [MaybeNullWhen(false)] out GitUrlInfo result)
    {
        Guard.IsNotNull(url);

        return TryCreate(url.AbsoluteUri, out result);
    }

    public static bool TryCreate(string url, [MaybeNullWhen(false)] out GitUrlInfo result)
    {
        // https://git-scm.com/docs/git-fetch#_git_urls
        if (string.IsNullOrEmpty(url))
        {
            result = null;
            return false;
        }

        // All recognized URLs contain at least one colon
        var firstColonPos = url.IndexOf(':', StringComparison.Ordinal);
        if (firstColonPos < 0)
        {
            result = null;
            return false;
        }

        var isRecognizedScheme = url.StartsWith("http://", StringComparison.Ordinal)
            || url.StartsWith("https://", StringComparison.Ordinal)
            || url.StartsWith("ssh://", StringComparison.Ordinal)
            || url.StartsWith("git://", StringComparison.Ordinal);
        if (!isRecognizedScheme)
        {
            if (url.StartsWith("file://", StringComparison.Ordinal))
            {
                // Common case that we may mistake for an SSH URL
                result = null;
                return false;
            }

            var firstSlashPos = url.IndexOf('/', StringComparison.Ordinal);
            if (firstSlashPos < 0 || firstColonPos < firstSlashPos)
            {
                // [user@]host.xz:path/to/repo.git/
                // Recognized by Git only if there are no slashes before the first colon
                url = "ssh://" + url[..firstColonPos] + "/" + url[(firstColonPos + 1)..];
            }
            else
            {
                result = null;
                return false;
            }
        }

        // Strip trailing slashes, if any
        url = url.TrimEnd('/');

        // Strip a trailing ".git" if present
        if (url.EndsWith(".git", StringComparison.Ordinal))
        {
            url = url[..^4];
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            result = null;
            return false;
        }

        result = new(
            new(url),
            host: uri.Host,
            port: uri.IsDefaultPort ? -1 : uri.Port,
            pathSegments: uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries));

        return true;
    }
}
