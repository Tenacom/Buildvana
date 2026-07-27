// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.HomeDirectory;

internal sealed class FixedHomeDirectoryProvider(string homeDirectory) : IHomeDirectoryProvider
{
    public string HomeDirectory => homeDirectory;
}
