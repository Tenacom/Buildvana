// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Core;
using Buildvana.Runtime;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// Bridges <c>Buildvana.Runtime</c> accessors into bv's error model.
/// </summary>
internal static class RuntimeAccess
{
    /// <summary>
    /// Runs a <c>Buildvana.Runtime</c> accessor, translating its <see cref="BuildvanaRuntimeException"/>
    /// into the <see cref="BuildFailedException"/> bv reports failures with.
    /// </summary>
    /// <typeparam name="T">The type of the accessed value.</typeparam>
    /// <param name="accessor">The accessor to run.</param>
    /// <returns>The accessed value.</returns>
    /// <exception cref="BuildFailedException">The accessor failed; the message is the accessor's own.</exception>
    /// <remarks>
    /// <para>Secret accessors such as <c>GetToken()</c> and <c>GetApiKey()</c> live in the Runtime library,
    /// so that hooks and bv resolve secrets through one code path; their failures, though, must surface as
    /// ordinary build failures rather than as an unhandled exception from a library bv happens to use. Every
    /// call into a throwing Runtime accessor goes through here, so the translation exists exactly once.</para>
    /// </remarks>
    public static T Translate<T>(Func<T> accessor)
    {
        Guard.IsNotNull(accessor);
        try
        {
            return accessor();
        }
        catch (BuildvanaRuntimeException e)
        {
            throw new BuildFailedException(e.Message, e);
        }
    }
}
