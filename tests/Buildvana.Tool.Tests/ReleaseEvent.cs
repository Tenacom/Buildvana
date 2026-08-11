// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/// <summary>
/// One observable step of a release, as recorded by <see cref="ReleaseHarness"/>, together with the state
/// of the repository at the moment it happened. The state is what makes the recording say something about
/// ordering: that the release commit already existed when the artifacts were packed, for example, or that
/// the push had already happened when the packages were pushed to NuGet.
/// </summary>
/// <param name="Name">The name of the step: the <c>dotnet</c> verb for a child process (with
/// <c>nuget push</c> reported as <c>nuget-push</c>), <c>hook</c>, or <c>publish</c>.</param>
/// <param name="HeadMessage">The message of the <c>HEAD</c> commit when the step happened.</param>
/// <param name="RemoteTipSha">The SHA of the tip of the branch on the remote when the step happened,
/// or <see langword="null"/> if nothing had been pushed yet.</param>
internal sealed record ReleaseEvent(string Name, string HeadMessage, string? RemoteTipSha);
