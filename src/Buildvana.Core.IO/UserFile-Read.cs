// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Buildvana.Core.IO;

partial class UserFile
{
    /// <summary>
    /// Opens a text file, reads all its text, and closes the file.
    /// </summary>
    /// <param name="path">The file to read from.</param>
    /// <returns>A string containing all the text in the file.</returns>
    /// <exception cref="BuildFailedException">The file could not be read.</exception>
    public static string ReadAllText(string path)
        => Read(path, () => File.ReadAllText(path));

    /// <summary>
    /// Opens a text file, reads all its text with the specified encoding, and closes the file.
    /// </summary>
    /// <param name="path">The file to read from.</param>
    /// <param name="encoding">The encoding applied to the contents of the file.</param>
    /// <returns>A string containing all the text in the file.</returns>
    /// <exception cref="BuildFailedException">The file could not be read.</exception>
    public static string ReadAllText(string path, Encoding encoding)
        => Read(path, () => File.ReadAllText(path, encoding));

    /// <summary>
    /// Asynchronously opens a text file, reads all its text, and closes the file.
    /// </summary>
    /// <param name="path">The file to read from.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is
    /// a string containing all the text in the file.</returns>
    /// <exception cref="BuildFailedException">The file could not be read.</exception>
    public static Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        => ReadAsync(path, () => File.ReadAllTextAsync(path, cancellationToken));

    /// <summary>
    /// Asynchronously opens a text file, reads all its text with the specified encoding, and closes the file.
    /// </summary>
    /// <param name="path">The file to read from.</param>
    /// <param name="encoding">The encoding applied to the contents of the file.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is
    /// a string containing all the text in the file.</returns>
    /// <exception cref="BuildFailedException">The file could not be read.</exception>
    public static Task<string> ReadAllTextAsync(string path, Encoding encoding, CancellationToken cancellationToken = default)
        => ReadAsync(path, () => File.ReadAllTextAsync(path, encoding, cancellationToken));

    /// <summary>
    /// Opens a text file, reads all its lines, and closes the file.
    /// </summary>
    /// <param name="path">The file to read from.</param>
    /// <returns>A string array containing all the lines of the file.</returns>
    /// <exception cref="BuildFailedException">The file could not be read.</exception>
    public static string[] ReadAllLines(string path)
        => Read(path, () => File.ReadAllLines(path));

    /// <summary>
    /// Opens a text file, reads all its lines with the specified encoding, and closes the file.
    /// </summary>
    /// <param name="path">The file to read from.</param>
    /// <param name="encoding">The encoding applied to the contents of the file.</param>
    /// <returns>A string array containing all the lines of the file.</returns>
    /// <exception cref="BuildFailedException">The file could not be read.</exception>
    public static string[] ReadAllLines(string path, Encoding encoding)
        => Read(path, () => File.ReadAllLines(path, encoding));

    /// <summary>
    /// Asynchronously opens a text file, reads all its lines, and closes the file.
    /// </summary>
    /// <param name="path">The file to read from.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is
    /// a string array containing all the lines of the file.</returns>
    /// <exception cref="BuildFailedException">The file could not be read.</exception>
    public static Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default)
        => ReadAsync(path, () => File.ReadAllLinesAsync(path, cancellationToken));

    /// <summary>
    /// Asynchronously opens a text file, reads all its lines with the specified encoding, and closes the file.
    /// </summary>
    /// <param name="path">The file to read from.</param>
    /// <param name="encoding">The encoding applied to the contents of the file.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is
    /// a string array containing all the lines of the file.</returns>
    /// <exception cref="BuildFailedException">The file could not be read.</exception>
    public static Task<string[]> ReadAllLinesAsync(string path, Encoding encoding, CancellationToken cancellationToken = default)
        => ReadAsync(path, () => File.ReadAllLinesAsync(path, encoding, cancellationToken));

    /// <summary>
    /// Opens a binary file, reads all its contents into a byte array, and closes the file.
    /// </summary>
    /// <param name="path">The file to read from.</param>
    /// <returns>A byte array containing the contents of the file.</returns>
    /// <exception cref="BuildFailedException">The file could not be read.</exception>
    public static byte[] ReadAllBytes(string path)
        => Read(path, () => File.ReadAllBytes(path));

    /// <summary>
    /// Asynchronously opens a binary file, reads all its contents into a byte array, and closes the file.
    /// </summary>
    /// <param name="path">The file to read from.</param>
    /// <param name="cancellationToken">A token that may be signalled to cancel the operation.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is
    /// a byte array containing the contents of the file.</returns>
    /// <exception cref="BuildFailedException">The file could not be read.</exception>
    public static Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        => ReadAsync(path, () => File.ReadAllBytesAsync(path, cancellationToken));
}
