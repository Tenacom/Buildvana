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
    /// Creates a new file, writes the specified string to it, and closes the file.
    /// If the file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to write to.</param>
    /// <param name="contents">The string to write to the file.</param>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static void WriteAllText(string path, string? contents)
        => Write(path, () => File.WriteAllText(path, contents));

    /// <summary>
    /// Creates a new file, writes the specified string to it with the specified encoding, and closes the file.
    /// If the file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to write to.</param>
    /// <param name="contents">The string to write to the file.</param>
    /// <param name="encoding">The encoding to apply to the string.</param>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static void WriteAllText(string path, string? contents, Encoding encoding)
        => Write(path, () => File.WriteAllText(path, contents, encoding));

    /// <summary>
    /// Asynchronously creates a new file, writes the specified string to it, and closes the file.
    /// If the file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to write to.</param>
    /// <param name="contents">The string to write to the file.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static Task WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default)
        => WriteAsync(path, () => File.WriteAllTextAsync(path, contents, cancellationToken));

    /// <summary>
    /// Asynchronously creates a new file, writes the specified string to it with the specified encoding,
    /// and closes the file. If the file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to write to.</param>
    /// <param name="contents">The string to write to the file.</param>
    /// <param name="encoding">The encoding to apply to the string.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static Task WriteAllTextAsync(string path, string? contents, Encoding encoding, CancellationToken cancellationToken = default)
        => WriteAsync(path, () => File.WriteAllTextAsync(path, contents, encoding, cancellationToken));

    /// <summary>
    /// Creates a new file, writes a collection of strings to it one per line, and closes the file.
    /// If the file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to write to.</param>
    /// <param name="contents">The lines to write to the file.</param>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static void WriteAllLines(string path, IEnumerable<string> contents)
        => Write(path, () => File.WriteAllLines(path, contents));

    /// <summary>
    /// Creates a new file, writes a collection of strings to it one per line with the specified encoding,
    /// and closes the file. If the file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to write to.</param>
    /// <param name="contents">The lines to write to the file.</param>
    /// <param name="encoding">The encoding to apply to each line.</param>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding)
        => Write(path, () => File.WriteAllLines(path, contents, encoding));

    /// <summary>
    /// Asynchronously creates a new file, writes a collection of strings to it one per line, and closes the file.
    /// If the file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to write to.</param>
    /// <param name="contents">The lines to write to the file.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static Task WriteAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken = default)
        => WriteAsync(path, () => File.WriteAllLinesAsync(path, contents, cancellationToken));

    /// <summary>
    /// Asynchronously creates a new file, writes a collection of strings to it one per line with the specified
    /// encoding, and closes the file. If the file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to write to.</param>
    /// <param name="contents">The lines to write to the file.</param>
    /// <param name="encoding">The encoding to apply to each line.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static Task WriteAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding, CancellationToken cancellationToken = default)
        => WriteAsync(path, () => File.WriteAllLinesAsync(path, contents, encoding, cancellationToken));

    /// <summary>
    /// Creates a new file, writes the specified byte array to it, and closes the file.
    /// If the file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to write to.</param>
    /// <param name="bytes">The bytes to write to the file.</param>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static void WriteAllBytes(string path, byte[] bytes)
        => Write(path, () => File.WriteAllBytes(path, bytes));

    /// <summary>
    /// Asynchronously creates a new file, writes the specified byte array to it, and closes the file.
    /// If the file already exists, it is overwritten.
    /// </summary>
    /// <param name="path">The file to write to.</param>
    /// <param name="bytes">The bytes to write to the file.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">The file could not be written.</exception>
    public static Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
        => WriteAsync(path, () => File.WriteAllBytesAsync(path, bytes, cancellationToken));
}
