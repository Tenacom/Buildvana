// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services;

partial class DotNetService
{
    private enum InvocationKind
    {
        /// <summary>
        /// A normal invocation: `dotnet` accepts the `--verbosity` argument and the user is interested in the output.
        /// </summary>
        Normal,

        /// <summary>
        /// An informational invocation: the user is interested in the output, but `dotnet` does not accept the `--verbosity` argument.
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        Informational,

        /// <summary>
        /// An internal invocation: the user is not interested in the output, and the `--verbosity` argument, if any, is already set appropriately.
        /// </summary>
        Internal,
    }
}
