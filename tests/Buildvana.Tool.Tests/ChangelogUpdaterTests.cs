// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Buildvana.Core;
using Buildvana.Tool.Services;

internal sealed class ChangelogUpdaterTests
{
    private const string NewSectionTitle = "[1.2.3](https://example.com/releases/tag/1.2.3) (2026-01-01)";

    [Test]
    public async Task HasUnreleasedChanges_IsFalseWhenSectionIsEmpty()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
            "### New features",
            string.Empty,
            "## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)",
            string.Empty,
            "- Something released.",
        ];

        await Assert.That(ChangelogUpdater.HasUnreleasedChanges(lines)).IsFalse();
    }

    [Test]
    public async Task HasUnreleasedChanges_IsTrueWhenSectionHasContent()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
            "### New features",
            string.Empty,
            "- Something new.",
            string.Empty,
            "## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)",
        ];

        await Assert.That(ChangelogUpdater.HasUnreleasedChanges(lines)).IsTrue();
    }

    // The "Unreleased changes" section is the last one before a published release exists;
    // reaching EOF without finding content is not a failure.
    [Test]
    public async Task HasUnreleasedChanges_IsFalseWhenEmptySectionEndsTheFile()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
            "### New features",
            string.Empty,
        ];

        await Assert.That(ChangelogUpdater.HasUnreleasedChanges(lines)).IsFalse();
    }

    [Test]
    public async Task HasUnreleasedChanges_FailsWhenThereAreNoSections()
    {
        string[] lines = ["# Changelog", string.Empty, "Nothing to see here."];
        bool Act() => ChangelogUpdater.HasUnreleasedChanges(lines);

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).IsEqualTo("CHANGELOG.md contains no sections.");
    }

    [Test]
    public async Task PrepareForRelease_MovesUnreleasedContentToNewSection()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
            "- Something new.",
            string.Empty,
            "## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)",
            string.Empty,
            "- Something released.",
        ];

        var result = PrepareForRelease(lines);

        await Assert.That(result).IsEqualTo(
            """
            # Changelog

            ## Unreleased changes

            ## [1.2.3](https://example.com/releases/tag/1.2.3) (2026-01-01)

            - Something new.

            ## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)

            - Something released.

            """.ReplaceLineEndings("\n"));
    }

    // Subsection headings are recreated (empty) in the "Unreleased changes" section, so that the next
    // release starts from the same skeleton; only those with actual content move to the new section.
    [Test]
    public async Task PrepareForRelease_RecreatesSubsectionsAndMovesOnlyNonEmptyOnes()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
            "### New features",
            string.Empty,
            "- Something new.",
            string.Empty,
            "### Bugs fixed in this release",
            string.Empty,
            "## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)",
        ];

        var result = PrepareForRelease(lines);

        await Assert.That(result).IsEqualTo(
            """
            # Changelog

            ## Unreleased changes

            ### New features

            ### Bugs fixed in this release

            ## [1.2.3](https://example.com/releases/tag/1.2.3) (2026-01-01)

            ### New features

            - Something new.

            ## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)

            """.ReplaceLineEndings("\n"));
    }

    // The substitute replaces the whole body of the moved section, blank lines included, so the blank
    // lines that set the new section apart are ours: however the substitute is padded in configuration,
    // the section comes out spaced like any other.
    [Test]
    public async Task PrepareForRelease_SubstitutesConfiguredTextForEmptySection()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
            "### New features",
            string.Empty,
            "## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)",
        ];

        var result = PrepareForRelease(lines, emptyChangelogSubstitute: "\n- Maintenance release.\r\n- No user-visible changes.\r\n\n");

        await Assert.That(result).IsEqualTo(
            """
            # Changelog

            ## Unreleased changes

            ### New features

            ## [1.2.3](https://example.com/releases/tag/1.2.3) (2026-01-01)

            - Maintenance release.
            - No user-visible changes.

            ## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)

            """.ReplaceLineEndings("\n"));
    }

    // The blank line added after the substitute is subject to the same EOF trimming as moved content.
    [Test]
    public async Task PrepareForRelease_SubstitutesConfiguredTextWhenNewSectionIsLast()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
        ];

        var result = PrepareForRelease(lines, emptyChangelogSubstitute: "- Maintenance release.");

        await Assert.That(result).IsEqualTo(
            """
            # Changelog

            ## Unreleased changes

            ## [1.2.3](https://example.com/releases/tag/1.2.3) (2026-01-01)

            - Maintenance release.

            """.ReplaceLineEndings("\n"));
    }

    // Without a substitute, an empty section moves verbatim: the new section gets a title and nothing else.
    [Test]
    public async Task PrepareForRelease_MovesEmptySectionVerbatimWithoutSubstitute()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
            "## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)",
        ];

        var result = PrepareForRelease(lines);

        await Assert.That(result).IsEqualTo(
            """
            # Changelog

            ## Unreleased changes

            ## [1.2.3](https://example.com/releases/tag/1.2.3) (2026-01-01)

            ## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)

            """.ReplaceLineEndings("\n"));
    }

    // A substitute that is all whitespace has nothing left to contribute once trimmed. Emitting it anyway would
    // write the blank lines that surround it and nothing else, i.e. three consecutive blank lines into a file
    // whose linter forbids them; it is treated as no substitute at all instead.
    [Test]
    public async Task PrepareForRelease_IgnoresWhitespaceOnlySubstitute()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
            "## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)",
        ];

        var result = PrepareForRelease(lines, emptyChangelogSubstitute: " \n\t\n ");

        await Assert.That(result).IsEqualTo(
            """
            # Changelog

            ## Unreleased changes

            ## [1.2.3](https://example.com/releases/tag/1.2.3) (2026-01-01)

            ## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)

            """.ReplaceLineEndings("\n"));
    }

    // When the new section is the last one in the file, the blank lines that separated the moved content
    // from the section that followed it would end up trailing at EOF, and are trimmed.
    [Test]
    public async Task PrepareForRelease_TrimsTrailingBlankLinesWhenNewSectionIsLast()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
            "- Something new.",
            string.Empty,
            string.Empty,
        ];

        var result = PrepareForRelease(lines);

        await Assert.That(result).IsEqualTo(
            """
            # Changelog

            ## Unreleased changes

            ## [1.2.3](https://example.com/releases/tag/1.2.3) (2026-01-01)

            - Something new.

            """.ReplaceLineEndings("\n"));
    }

    // The rewritten changelog always uses LF, whatever the line endings of the file it was read from
    // (which never reach this code), of the configured substitute (which does), and of the platform it runs on.
    [Test]
    public async Task PrepareForRelease_UsesLineFeedAsLineSeparator()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
            "## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)",
        ];

        var result = PrepareForRelease(lines, emptyChangelogSubstitute: "- Maintenance release.\r\n- No user-visible changes.");

        await Assert.That(result).DoesNotContain("\r");
        await Assert.That(result).EndsWith("\n");
    }

    [Test]
    public async Task PrepareForRelease_FailsWhenThereAreNoSections()
    {
        string[] lines = ["# Changelog", string.Empty, "Nothing to see here."];
        string Act() => PrepareForRelease(lines);

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).IsEqualTo("CHANGELOG.md contains no sections.");
    }

    // The title is only needed when a new section is actually written: a changelog that fails validation
    // must report that failure, not one raised while composing a title that will never be used.
    [Test]
    public async Task PrepareForRelease_DoesNotMakeSectionTitleWhenThereAreNoSections()
    {
        string[] lines = ["# Changelog", string.Empty, "Nothing to see here."];
        var titleMade = false;
        string MakeSectionTitle()
        {
            titleMade = true;
            return NewSectionTitle;
        }

        string Act() => ChangelogUpdater.PrepareForRelease(lines, MakeSectionTitle, null);

        _ = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(titleMade).IsFalse();
    }

    [Test]
    public async Task UpdateNewSectionTitle_ReplacesTitleOfSectionAfterUnreleasedChanges()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
            "### New features",
            string.Empty,
            "## [1.2.3-preview.1](https://example.com/releases/tag/1.2.3-preview.1) (2026-01-01)",
            string.Empty,
            "- Something new.",
            string.Empty,
            "## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)",
        ];

        var result = ChangelogUpdater.UpdateNewSectionTitle(lines, () => NewSectionTitle);

        await Assert.That(result).IsEqualTo(
            """
            # Changelog

            ## Unreleased changes

            ### New features

            ## [1.2.3](https://example.com/releases/tag/1.2.3) (2026-01-01)

            - Something new.

            ## [1.2.2](https://example.com/releases/tag/1.2.2) (2025-12-01)

            """.ReplaceLineEndings("\n"));
    }

    [Test]
    public async Task UpdateNewSectionTitle_FailsWhenThereIsOnlyOneSection()
    {
        string[] lines =
        [
            "# Changelog",
            string.Empty,
            "## Unreleased changes",
            string.Empty,
            "- Something new.",
        ];

        string Act() => ChangelogUpdater.UpdateNewSectionTitle(lines, () => NewSectionTitle);

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).IsEqualTo("CHANGELOG.md contains only one section.");
    }

    [Test]
    public async Task UpdateNewSectionTitle_FailsWhenThereAreNoSections()
    {
        string[] lines = ["# Changelog", string.Empty, "Nothing to see here."];
        string Act() => ChangelogUpdater.UpdateNewSectionTitle(lines, () => NewSectionTitle);

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).IsEqualTo("CHANGELOG.md contains no sections.");
    }

    // Same guarantee as PrepareForRelease: the title is composed only when a heading is actually replaced.
    [Test]
    public async Task UpdateNewSectionTitle_DoesNotMakeSectionTitleWhenThereAreNoSections()
    {
        string[] lines = ["# Changelog", string.Empty, "Nothing to see here."];
        var titleMade = false;
        string MakeSectionTitle()
        {
            titleMade = true;
            return NewSectionTitle;
        }

        string Act() => ChangelogUpdater.UpdateNewSectionTitle(lines, MakeSectionTitle);

        _ = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(titleMade).IsFalse();
    }

    [Test]
    public async Task MakeSectionTitle_ComposesVersionUrlAndDate()
    {
        var title = ComposeSectionTitle();

        await Assert.That(title).IsEqualTo(NewSectionTitle);
    }

    // The title ends up in a file that is read, and released from, on every machine: the date must not follow
    // the calendar of whoever runs the release. Formatted with the current culture, it would read 2569-01-01
    // under th-TH, matching neither the release tag nor the other section titles.
    [Test]
    [NotInParallel]
    public async Task MakeSectionTitle_UsesInvariantCalendar_WhateverTheCurrentCulture()
    {
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            var title = ComposeSectionTitle();

            await Assert.That(title).IsEqualTo(NewSectionTitle);
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    private static string ComposeSectionTitle()
        => ChangelogUpdater.MakeSectionTitle("1.2.3", new Uri("https://example.com/releases/tag/1.2.3"), new DateTime(2026, 1, 1));

    private static string PrepareForRelease(string[] lines, string? emptyChangelogSubstitute = null)
        => ChangelogUpdater.PrepareForRelease(lines, () => NewSectionTitle, emptyChangelogSubstitute);
}
