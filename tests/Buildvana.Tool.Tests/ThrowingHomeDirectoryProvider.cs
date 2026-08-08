// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.HomeDirectory;

/// <summary>
/// A home directory provider whose discovery always fails, for tests exercising failure paths.
/// </summary>
internal sealed class ThrowingHomeDirectoryProvider : IHomeDirectoryProvider
{
    public string HomeDirectory => throw new BuildFailedException("Home directory not defined.");
}
