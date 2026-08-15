// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Runtime;

// The variable names are unique to each test run, so the tests are parallel-safe without [NotInParallel]:
// nothing else can observe or collide with them.
internal sealed class NuGetFeedConfigExtensionsTests
{
    [Test]
    public async Task GetApiKey_ReadsTheNamedVariable()
    {
        var name = "BV_TEST_API_KEY_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(name, "the-api-key");
        try
        {
            await Assert.That(Feed(name).GetApiKey()).IsEqualTo("the-api-key");
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Test]
    public async Task GetApiKey_Throws_WhenTheVariableIsMissing()
    {
        var name = "BV_TEST_API_KEY_" + Guid.NewGuid().ToString("N");
        await Assert.That(() => Feed(name).GetApiKey()).Throws<BuildvanaRuntimeException>();
    }

    private static NuGetFeedConfig Feed(string apiKeyEnv) => new()
    {
        Source = "https://nuget.example/v3/index.json",
        ApiKeyEnv = apiKeyEnv,
    };
}
