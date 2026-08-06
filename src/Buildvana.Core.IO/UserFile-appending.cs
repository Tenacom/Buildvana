// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Buildvana.Core.IO;

partial class UserFile
{
    /// <summary>
    /// Appends the specified string to a file, creating the file if it does not exist, then closes the file.
    /// </summary>
    /// <param name="path">The file to append to.</param>
    /// <param name="contents">The string to append to the file.</param>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static void AppendAllText(string path, string? contents)
        => Write(path, () => File.AppendAllText(path, contents));

    /// <summary>
    /// Appends the specified string to a file with the specified encoding, creating the file if it does not exist,
    /// then closes the file.
    /// </summary>
    /// <param name="path">The file to append to.</param>
    /// <param name="contents">The string to append to the file.</param>
    /// <param name="encoding">The encoding to apply to the string.</param>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static void AppendAllText(string path, string? contents, Encoding encoding)
        => Write(path, () => File.AppendAllText(path, contents, encoding));

    /// <summary>
    /// Asynchronously appends the specified string to a file, creating the file if it does not exist,
    /// then closes the file.
    /// </summary>
    /// <param name="path">The file to append to.</param>
    /// <param name="contents">The string to append to the file.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static Task AppendAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default)
        => WriteAsync(path, () => File.AppendAllTextAsync(path, contents, cancellationToken));

    /// <summary>
    /// Asynchronously appends the specified string to a file with the specified encoding, creating the file
    /// if it does not exist, then closes the file.
    /// </summary>
    /// <param name="path">The file to append to.</param>
    /// <param name="contents">The string to append to the file.</param>
    /// <param name="encoding">The encoding to apply to the string.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static Task AppendAllTextAsync(string path, string? contents, Encoding encoding, CancellationToken cancellationToken = default)
        => WriteAsync(path, () => File.AppendAllTextAsync(path, contents, encoding, cancellationToken));

    /// <summary>
    /// Appends a collection of strings to a file one per line, creating the file if it does not exist,
    /// then closes the file.
    /// </summary>
    /// <param name="path">The file to append to.</param>
    /// <param name="contents">The lines to append to the file.</param>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    /// <remarks>
    /// <para><paramref name="contents"/> is enumerated inside the guarded region: a failure raised by a
    /// lazily-evaluated collection is reported, misleadingly, as a failure to write to <paramref name="path"/>.
    /// Prefer materialized collections.</para>
    /// </remarks>
    public static void AppendAllLines(string path, IEnumerable<string> contents)
        => Write(path, () => File.AppendAllLines(path, contents));

    /// <summary>
    /// Appends a collection of strings to a file one per line with the specified encoding, creating the file
    /// if it does not exist, then closes the file.
    /// </summary>
    /// <param name="path">The file to append to.</param>
    /// <param name="contents">The lines to append to the file.</param>
    /// <param name="encoding">The encoding to apply to each line.</param>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    /// <remarks>
    /// <para><paramref name="contents"/> is enumerated inside the guarded region: a failure raised by a
    /// lazily-evaluated collection is reported, misleadingly, as a failure to write to <paramref name="path"/>.
    /// Prefer materialized collections.</para>
    /// </remarks>
    public static void AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding)
        => Write(path, () => File.AppendAllLines(path, contents, encoding));

    /// <summary>
    /// Asynchronously appends a collection of strings to a file one per line, creating the file
    /// if it does not exist, then closes the file.
    /// </summary>
    /// <param name="path">The file to append to.</param>
    /// <param name="contents">The lines to append to the file.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    /// <remarks>
    /// <para><paramref name="contents"/> is enumerated inside the guarded region: a failure raised by a
    /// lazily-evaluated collection is reported, misleadingly, as a failure to write to <paramref name="path"/>.
    /// Prefer materialized collections.</para>
    /// </remarks>
    public static Task AppendAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken = default)
        => WriteAsync(path, () => File.AppendAllLinesAsync(path, contents, cancellationToken));

    /// <summary>
    /// Asynchronously appends a collection of strings to a file one per line with the specified encoding,
    /// creating the file if it does not exist, then closes the file.
    /// </summary>
    /// <param name="path">The file to append to.</param>
    /// <param name="contents">The lines to append to the file.</param>
    /// <param name="encoding">The encoding to apply to each line.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    /// <remarks>
    /// <para><paramref name="contents"/> is enumerated inside the guarded region: a failure raised by a
    /// lazily-evaluated collection is reported, misleadingly, as a failure to write to <paramref name="path"/>.
    /// Prefer materialized collections.</para>
    /// </remarks>
    public static Task AppendAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding, CancellationToken cancellationToken = default)
        => WriteAsync(path, () => File.AppendAllLinesAsync(path, contents, encoding, cancellationToken));
}
