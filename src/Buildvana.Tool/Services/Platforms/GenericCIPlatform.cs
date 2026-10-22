// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Buildvana.Tool.Services.Git;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Platforms;

/// <summary>
/// A CI runner that no other platform recognizes.
/// </summary>
/// <remarks>
/// <para>The runner is recognized as the .NET CLI recognizes a CI environment, for the default of its
/// <c>--interactive</c> option: see <c>CIEnvironmentDetectorForTelemetry</c> and <c>EnvironmentVariableParser.ParseBool</c>
/// in the dotnet/sdk repository. The variables and the values that read as true are taken from there.</para>
/// </remarks>
internal sealed class GenericCIPlatform : CIPlatform
{
    // Variables that mark a CI runner when their value reads as true.
    private static readonly string[] BooleanVariables = ["TF_BUILD", "GITHUB_ACTIONS", "APPVEYOR", "CI", "TRAVIS", "CIRCLECI"];

    // The values that read as true, in any case.
    private static readonly string[] TrueValues = ["1", "true", "yes", "on"];

    // Sets of variables that mark a CI runner when every variable of the set is non-empty.
    private static readonly string[][] PresentVariableSets = [
        ["CODEBUILD_BUILD_ID", "AWS_REGION"],
        ["BUILD_ID", "BUILD_URL"],
        ["BUILD_ID", "PROJECT_ID"],
        ["TEAMCITY_VERSION"],
        ["JB_SPACE_API_URL"],
    ];

    private GenericCIPlatform()
    {
    }

    /// <inheritdoc/>
    public override string Name => "Generic CI";

    /// <inheritdoc/>
    /// <value>Always <see langword="true"/>.</value>
    public override bool IsCI => true;

    /// <inheritdoc/>
    /// <value>Always <see langword="null"/>: a runner <c>bv</c> knows nothing about has no known bot.</value>
    public override GitIdentity? BotIdentity => null;

    /// <summary>
    /// Creates and returns an instance of <see cref="GenericCIPlatform"/> if <c>bv</c> runs in a CI runner
    /// the .NET CLI recognizes.
    /// </summary>
    /// <param name="getEnvironmentVariable">A function that returns the value of an environment variable,
    /// or <see langword="null"/> when the variable is not set.</param>
    /// <returns>If the environment marks a CI runner, a newly-created <see cref="GenericCIPlatform"/>;
    /// otherwise, <see langword="null"/>.</returns>
    public static CIPlatform? DetectIfApplicable(Func<string, string?> getEnvironmentVariable)
    {
        Guard.IsNotNull(getEnvironmentVariable);
        var isCI = BooleanVariables.Any(x => IsTrue(getEnvironmentVariable(x)))
            || PresentVariableSets.Any(set => set.All(x => !string.IsNullOrEmpty(getEnvironmentVariable(x))));
        return isCI ? new GenericCIPlatform() : null;
    }

    private static bool IsTrue(string? value) => value is not null && TrueValues.Contains(value, StringComparer.OrdinalIgnoreCase);
}
