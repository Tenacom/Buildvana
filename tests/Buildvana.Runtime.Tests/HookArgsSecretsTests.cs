// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Runtime;

// The configuration embedded in hook args names the environment variables that hold credentials; the values
// are read on demand, through extension methods, which serialization cannot see. Writing args the way bv
// writes them must therefore never leak a credential into the args file, whatever the environment holds at
// the time. The variable name is unique to the test run, so no [NotInParallel] is needed: nothing else can
// observe it.
internal sealed class HookArgsSecretsTests
{
    [Test]
    public async Task Serialize_WritesCredentialVariableNames_NeverTheirValues()
    {
        var name = "BV_TEST_SECRET_" + Guid.NewGuid().ToString("N");
        const string sentinel = "s3cr3t-sentinel-value";
        Environment.SetEnvironmentVariable(name, sentinel);
        try
        {
            object args = SampleArgs(name);
            var json = JsonSerializer.Serialize(args, args.GetType(), BuildvanaJsonContext.Default);

            await Assert.That(json).Contains(name);
            await Assert.That(json).DoesNotContain(sentinel);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    private static PostReleaseHookArgs SampleArgs(string credentialEnvVar) => new()
    {
        RuntimeInfo = new()
        {
            Version = "1.2.3",
            HomeDirectory = "/repo",
            ArtifactsDirectory = "/repo/artifacts/Release",
            ScratchDirectory = "/repo/.buildvana-temp",
            ConfigFile = null,
            Configuration = new()
            {
                GitHub = new() { TokenEnv = credentialEnvVar },
                NuGet = new()
                {
                    Feeds = new()
                    {
                        Release = new() { Source = "https://nuget.example/v3/index.json", ApiKeyEnv = credentialEnvVar },
                    },
                },
            },
        },
        Release = new()
        {
            Version = "1.2.3",
            SemVer = "1.2.3",
            PreviousVersion = null,
            IsPrerelease = false,
            IsPublicRelease = true,
        },
        ProducedPackages = new Dictionary<string, string>(),
        Dogfooding = false,
    };
}
