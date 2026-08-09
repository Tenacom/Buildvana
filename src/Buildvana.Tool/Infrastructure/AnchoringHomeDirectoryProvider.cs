// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.HomeDirectory;
using Buildvana.Core.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Infrastructure;

/// <summary>
/// Decorates a home directory provider, making the home directory the process's current directory
/// at the moment of discovery. From that point on, every relative path in the process — including
/// relative paths in forwarded arguments — resolves against the home directory no matter where
/// <c>bv</c> was invoked from, matching delegated runs, which are spawned from the home directory.
/// If discovery fails, the current directory is left untouched.
/// </summary>
/// <remarks>
/// <para>The anchoring happens on the first read of <see cref="HomeDirectoryProvider.HomeDirectory"/>, which
/// is when discovery runs — not at construction. This laziness is the point, not an accident: a run that never
/// needs a home directory (<c>bv --version</c>, a delegating run, an invocation outside a repository) must
/// neither fail nor move the current directory. Anchoring eagerly would trade that for a guarantee with an
/// earlier start time, and would make "not in a repository" the first thing every command reports.</para>
/// <para>The trade-off is that the moment the current directory changes is decided by whoever reads
/// <see cref="HomeDirectoryProvider.HomeDirectory"/> first. Code that resolves a relative path against the
/// current directory before that read would resolve it against the invocation directory instead; resolve
/// repository-relative paths through <see cref="HomeDirectoryProviderExtensions.GetFullPath"/> rather than
/// against ambient process state, and the question does not arise.</para>
/// </remarks>
internal sealed class AnchoringHomeDirectoryProvider : HomeDirectoryProvider
{
    private readonly IHomeDirectoryProvider _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnchoringHomeDirectoryProvider"/> class.
    /// </summary>
    /// <param name="inner">The provider that performs the actual discovery.</param>
    public AnchoringHomeDirectoryProvider(IHomeDirectoryProvider inner)
    {
        Guard.IsNotNull(inner);
        _inner = inner;
    }

    /// <inheritdoc />
    protected override string Resolve()
    {
        var homeDirectory = _inner.HomeDirectory;
        UserDirectory.SetCurrentDirectory(homeDirectory);
        return homeDirectory;
    }
}
