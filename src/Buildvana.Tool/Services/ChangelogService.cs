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
/// Manages the repository's changelog in Markdown format, according to the
/// <see href="https://keepachangelog.com/en/1.1.0/">Keep a Changelog</see> specification.
/// </summary>
/// <remarks>
/// <para>The changelog is always read in a single guarded call, so that an I/O failure at any point of the read —
/// not just when opening the file — is reported as a clean error instead of an unhandled exception. Parsing and
/// rewriting are left to <see cref="ChangelogUpdater"/>.</para>
/// </remarks>
internal sealed class ChangelogService
{
    /// <summary>
    /// The name of the changelog file.
    /// </summary>
    public const string FileName = "CHANGELOG.md";

    // The changelog is written without a BOM, and invalid UTF-8 is an error rather than something
    // to paper over with replacement characters: a changelog that cannot be read faithfully cannot
    // be rewritten faithfully either. The strict decoding is best-effort on reads, though: the file
    // APIs detect byte order marks, and a detected BOM replaces this encoding with a stock one whose
    // fallback substitutes U+FFFD. A changelog that carries a BOM and invalid bytes is therefore read
    // and rewritten with replacement characters, as it was before this encoding was introduced.
    private static readonly Encoding FileEncoding = new UTF8Encoding(false, true);

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

        var lines = UserFile.ReadAllLines(FileName, FileEncoding);
        return ChangelogUpdater.HasUnreleasedChanges(lines);
    }

    /// <summary>
    /// Prepares the changelog for a new release by moving the contents of the "Unreleased changes" section
    /// to a new section.
    /// </summary>
    /// <param name="emptyChangelogSubstitute">Text to use as the new section's body when the "Unreleased changes"
    /// section has no content. When <see langword="null"/>, or all whitespace, an empty section is moved
    /// verbatim (producing a title-only section).</param>
    public void PrepareForRelease(string? emptyChangelogSubstitute = null)
    {
        _reporter.Info("Updating changelog...");
        var lines = UserFile.ReadAllLines(FileName, FileEncoding);
        var text = ChangelogUpdater.PrepareForRelease(lines, MakeSectionTitle, emptyChangelogSubstitute);
        UserFile.WriteAllText(FileName, text, FileEncoding);
    }

    /// <summary>
    /// Updates the heading of the first section of the changelog after the "Unreleased changes" section
    /// to reflect a change in the released version.
    /// </summary>
    public void UpdateNewSectionTitle()
    {
        _reporter.Info("Updating changelog's new release section title...");
        var lines = UserFile.ReadAllLines(FileName, FileEncoding);
        var text = ChangelogUpdater.UpdateNewSectionTitle(lines, MakeSectionTitle);
        UserFile.WriteAllText(FileName, text, FileEncoding);
    }

    private string MakeSectionTitle()
        => ChangelogUpdater.MakeSectionTitle(_version.CurrentStr, _server.GetReleaseUrl(_version.CurrentStr), DateTime.Now);
}
