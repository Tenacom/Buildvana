// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Security;

namespace Buildvana.Runtime;

/// <summary>
/// Provides extension members for <see cref="Exception"/> instances.
/// </summary>
internal static class ExceptionExtensions
{
    extension(Exception @this)
    {
        /// <summary>
        /// Gets a value indicating whether the exception is one the environment raises when it denies
        /// or fails an I/O operation, including the malformed-path family.
        /// </summary>
        /// <remarks>
        /// <para>Kept in sync by hand with <c>Buildvana.Core.Diagnostics.ExceptionExtensions.IsIORelatedException</c>,
        /// the source of truth for this classification; it cannot be referenced here because
        /// <c>Buildvana.Runtime</c>'s dependency closure must stay BCL-only.</para>
        /// </remarks>
        public bool IsIORelatedException
            => @this is UnauthorizedAccessException
                or NotSupportedException
                or (ArgumentException and not ArgumentNullException)
                or SecurityException
                or IOException;
    }
}
