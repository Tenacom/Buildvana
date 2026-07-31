// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;

internal sealed class GitHubRepositoryUrlsTests
{
    private static GitHubRepositoryUrls Urls => new("github.com", "Tenacom", "Buildvana");

    [Test]
    public async Task Repository_HasNoTrailingSlash()
    {
        await Assert.That(Urls.Repository.ToString()).IsEqualTo("https://github.com/Tenacom/Buildvana");
    }

    [Test]
    public async Task ReleaseTag_SeparatesRepositoryNameFromFirstSegment()
    {
        // Regression: this used to come out as ".../Tenacom/Buildvanareleases/tag/2.1.2-preview".
        var url = Urls.ReleaseTag("2.1.2-preview").ToString();
        await Assert.That(url).IsEqualTo("https://github.com/Tenacom/Buildvana/releases/tag/2.1.2-preview");
    }

    [Test]
    public async Task File_SeparatesRepositoryNameFromFirstSegment()
    {
        // Regression: this used to come out as ".../Tenacom/Buildvanablob/main/CHANGELOG.md".
        var url = Urls.File("CHANGELOG.md", "main").ToString();
        await Assert.That(url).IsEqualTo("https://github.com/Tenacom/Buildvana/blob/main/CHANGELOG.md");
    }

    [Test]
    public async Task File_AcceptsCommitShaAsCommitish()
    {
        var url = Urls.File("CHANGELOG.md", "7f7aebf19f12da3c8e6335b16cc7d5482b7b70a6").ToString();
        await Assert.That(url).IsEqualTo("https://github.com/Tenacom/Buildvana/blob/7f7aebf19f12da3c8e6335b16cc7d5482b7b70a6/CHANGELOG.md");
    }

    [Test]
    public async Task File_NormalizesBackslashesToForwardSlashes()
    {
        var url = Urls.File(@"docs\ConstantsSyntax.md", "main").ToString();
        await Assert.That(url).IsEqualTo("https://github.com/Tenacom/Buildvana/blob/main/docs/ConstantsSyntax.md");
    }

    [Test]
    public async Task Urls_HonorNonDefaultHostName()
    {
        var urls = new GitHubRepositoryUrls("github.example.com", "Contoso", "Widgets");
        await Assert.That(urls.Repository.ToString()).IsEqualTo("https://github.example.com/Contoso/Widgets");
        await Assert.That(urls.ReleaseTag("1.0.0").ToString()).IsEqualTo("https://github.example.com/Contoso/Widgets/releases/tag/1.0.0");
        await Assert.That(urls.File("README.md", "main").ToString()).IsEqualTo("https://github.example.com/Contoso/Widgets/blob/main/README.md");
    }

    [Test]
    [Arguments("..")]
    [Arguments("../outside.md")]
    [Arguments(@"..\outside.md")]
    [Arguments("docs/..")]
    [Arguments("docs/nested/../ConstantsSyntax.md")]
    [Arguments("docs/../../outside.md")]
    [Arguments("docs/../../../../../../etc/passwd")]
    public async Task File_RejectsPathWithParentSegment(string path)
    {
        // A parent segment anywhere is rejected, not just a leading one: Uri collapses them as it parses,
        // so the last case above would otherwise resolve to "https://github.com/etc/passwd".
        await Assert.That(() => Urls.File(path, "main")).Throws<ArgumentException>();
    }

    [Test]
    [Arguments("..gitignore.md")]
    [Arguments("docs/..hidden/file.md")]
    public async Task File_AcceptsNameStartingWithTwoDots(string path)
    {
        // Only a whole ".." segment escapes; two dots at the start of a name are just a name.
        var url = Urls.File(path, "main").ToString();
        await Assert.That(url).IsEqualTo($"https://github.com/Tenacom/Buildvana/blob/main/{path}");
    }

    [Test]
    public async Task File_RejectsFullyQualifiedPath()
    {
        // Built from a relative path so the test means the same thing on every platform.
        var fullyQualified = Path.GetFullPath("CHANGELOG.md");
        await Assert.That(() => Urls.File(fullyQualified, "main")).Throws<ArgumentException>();
    }

    [Test]
    public async Task ReleaseTag_RejectsEmptyVersion()
    {
        await Assert.That(() => Urls.ReleaseTag(string.Empty)).Throws<ArgumentException>();
    }

    [Test]
    public async Task File_RejectsEmptyPath()
    {
        await Assert.That(() => Urls.File(string.Empty, "main")).Throws<ArgumentException>();
    }

    [Test]
    public async Task File_RejectsEmptyCommitish()
    {
        await Assert.That(() => Urls.File("CHANGELOG.md", string.Empty)).Throws<ArgumentException>();
    }

    [Test]
    [Arguments("", "Tenacom", "Buildvana")]
    [Arguments("github.com", "", "Buildvana")]
    [Arguments("github.com", "Tenacom", "")]
    public async Task Constructor_RejectsEmptyArguments(string hostName, string owner, string name)
    {
        await Assert.That(() => new GitHubRepositoryUrls(hostName, owner, name)).Throws<ArgumentException>();
    }
}
