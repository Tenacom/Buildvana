// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.IO;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Versioning;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services;

/// <summary>
/// Manages the repository's changelog in Markdown format, according to the <see href="https://keepachangelog.com/en/1.1.0/">Keep a Changelog</see> specification.
/// </summary>
internal sealed class ChangelogService
{
    /// <summary>
    /// The name of the changelog file.
    /// </summary>
    public const string FileName = "CHANGELOG.md";

    private readonly IReporter _reporter;
    private readonly ServerAdapter _server;
    private readonly VersionService _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangelogService"/> class.
    /// </summary>
    public ChangelogService(IReporter reporter, ServerAdapter server, VersionService version)
    {
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(server);
        Guard.IsNotNull(version);
        _reporter = reporter;
        _server = server;
        _version = version;
        Exists = File.Exists(FileName);
    }

    /// <summary>
    /// Gets a value indicating whether the changelog file exists.
    /// </summary>
    public bool Exists { get; }

    /// <summary>
    /// Checks the changelog for contents in the "Unreleased changes" section.
    /// </summary>
    /// <returns>If there are any contents (excluding blank lines and subsection headings)
    /// in the "Unreleased changes" section, <see langword="true"/>; otherwise, <see langword="false"/>.</returns>
    public bool HasUnreleasedChanges()
    {
        if (!Exists)
        {
            return false;
        }

        // Unlike the methods that rewrite the changelog, this one only needs a prefix of the file:
        // it stops at the first line of content in the "Unreleased changes" section. Streaming the file
        // is therefore worth the narrower guarantee of OpenText, which guards the open but not the reads.
        using var reader = UserFile.OpenText(FileName, Encoding.UTF8);
        return ChangelogUpdater.HasUnreleasedChanges(reader);
    }

    /// <summary>
    /// Prepares the changelog for a new release by moving the contents of the "Unreleased changes" section
    /// to a new section.
    /// </summary>
    /// <param name="emptyChangelogSubstitute">Text to use as the new section's body when the "Unreleased changes"
    /// section has no content. When <see langword="null"/>, an empty section is moved verbatim (producing a
    /// title-only section).</param>
    public void PrepareForRelease(string? emptyChangelogSubstitute = null)
    {
        _reporter.Info("Updating changelog...");
        var encoding = new UTF8Encoding(false, true);

        // The whole changelog is rewritten anyway, so it is read in one guarded call:
        // an I/O failure at any point is then reported as a clean error, not as an unhandled exception.
        var lines = UserFile.ReadAllLines(FileName, encoding);
        var text = ChangelogUpdater.PrepareForRelease(lines, MakeSectionTitle, emptyChangelogSubstitute);
        UserFile.WriteAllText(FileName, text, encoding);
    }

    /// <summary>
    /// Updates the heading of the first section of the changelog after the "Unreleased changes" section
    /// to reflect a change in the released version.
    /// </summary>
    public void UpdateNewSectionTitle()
    {
        _reporter.Info("Updating changelog's new release section title...");
        var encoding = new UTF8Encoding(false, true);

        // The whole changelog is rewritten anyway, so it is read in one guarded call:
        // an I/O failure at any point is then reported as a clean error, not as an unhandled exception.
        var lines = UserFile.ReadAllLines(FileName, encoding);
        var text = ChangelogUpdater.UpdateNewSectionTitle(lines, MakeSectionTitle);
        UserFile.WriteAllText(FileName, text, encoding);
    }

    private string MakeSectionTitle()
        => $"[{_version.CurrentStr}]({_server.GetReleaseUrl(_version.CurrentStr)}) ({DateTime.Now:yyyy-MM-dd})";
}
