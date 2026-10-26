// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using Buildvana.Core.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Platforms;

/// <summary>
/// Writes named step outputs to the file a GitHub Actions runner reads them from.
/// </summary>
/// <remarks>
/// <para>The file format is the one GitHub documents for <c>GITHUB_OUTPUT</c>: a single-line value goes in
/// a <c>name=value</c> line, and a multi-line value goes between a <c>name&lt;&lt;delimiter</c> line and a
/// <c>delimiter</c> line.</para>
/// <para>Gitea and Forgejo runners read the same format, from <c>GITHUB_OUTPUT</c> and <c>FORGEJO_OUTPUT</c>.</para>
/// </remarks>
internal sealed class GitHubActionsStepOutputWriter : StepOutputWriter
{
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubActionsStepOutputWriter"/> class.
    /// </summary>
    /// <param name="path">The path of the file that collects the step outputs.</param>
    public GitHubActionsStepOutputWriter(string path)
    {
        Guard.IsNotNullOrEmpty(path);
        _path = path;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty, or contains a character that
    /// would end the name before its end: <c>=</c>, <c>&lt;</c>, or a line break.</exception>
    public override void Write(string name, string value)
    {
        Guard.IsNotNullOrEmpty(name);
        Guard.IsNotNull(value);
        if (name.AsSpan().IndexOfAny("=<\r\n") >= 0)
        {
            ThrowHelper.ThrowArgumentException(nameof(name), $"Step output name '{name}' contains '=', '<', or a line break.");
        }

        if (value.AsSpan().IndexOfAny('\r', '\n') < 0)
        {
            UserFile.AppendAllLines(_path, [$"{name}={value}"]);
            return;
        }

        // A random delimiter cannot occur in the value by accident.
        var delimiter = string.Create(CultureInfo.InvariantCulture, $"bv_{Guid.NewGuid():N}");
        UserFile.AppendAllLines(_path, [$"{name}<<{delimiter}", value, delimiter]);
    }
}
