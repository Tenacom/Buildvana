// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.HomeDirectory;

namespace Buildvana.Core.Testing;

/// <summary>
/// A home directory provider whose discovery always fails, for tests exercising failure paths.
/// Fails the way real providers do: from <see cref="Resolve"/>, on first read of
/// <see cref="HomeDirectoryProvider.HomeDirectory"/>.
/// </summary>
public sealed class ThrowingHomeDirectoryProvider : HomeDirectoryProvider
{
    /// <inheritdoc/>
    /// <exception cref="BuildFailedException">Always thrown.</exception>
    protected override string Resolve() => throw new BuildFailedException("Home directory not defined.");
}
