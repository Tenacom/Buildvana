// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using Buildvana.Core;
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
    /// Finalizes the first section of the changelog after the "Unreleased changes" section, once the released
    /// version is known: updates its heading, and pins each relative file link in it to the release tag.
    /// </summary>
    public void FinalizeNewSection()
    {
        _reporter.Info("Finalizing changelog's new release section...");
        var lines = UserFile.ReadAllLines(FileName, FileEncoding);
        var text = ChangelogUpdater.FinalizeNewSection(lines, MakeSectionTitle, GetFileUrl);
        UserFile.WriteAllText(FileName, text, FileEncoding);
    }

    private string MakeSectionTitle()
        => ChangelogUpdater.MakeSectionTitle(_version.CurrentStr, _server.GetReleaseUrl(_version.CurrentStr), DateTime.Now);

    // The release tag is named after the version, so the version string is the commitish the links pin to.
    // The adapter rejects a path that leaves the repository, or a rooted one, with an ArgumentException. The
    // path comes from a changelog bullet, so the rejection is reported as a build failure that names it.
    private Uri GetFileUrl(string path)
    {
        try
        {
            return _server.GetFileUrl(path, _version.CurrentStr);
        }
        catch (ArgumentException e)
        {
            throw new BuildFailedException($"{FileName} links '{path}', which is not a path to a file in the repository.", e);
        }
    }
}
