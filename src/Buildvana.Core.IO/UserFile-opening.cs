// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Text;

namespace Buildvana.Core.IO;

partial class UserFile
{
    /// <summary>
    /// Opens an existing file for reading.
    /// </summary>
    /// <param name="path">The file to open for reading.</param>
    /// <returns>A read-only <see cref="FileStream"/> on the specified file.</returns>
    /// <exception cref="BuildFailedException">The file could not be opened.</exception>
    /// <remarks>
    /// <para>Only the opening of the file is guarded: failures raised by subsequent operations
    /// on the returned stream surface as their original exceptions.</para>
    /// </remarks>
    public static FileStream OpenRead(string path)
        => Read(path, () => File.OpenRead(path));

    /// <summary>
    /// Opens an existing UTF-8 encoded text file for reading.
    /// </summary>
    /// <param name="path">The file to open for reading.</param>
    /// <returns>A <see cref="StreamReader"/> on the specified file.</returns>
    /// <exception cref="BuildFailedException">The file could not be opened.</exception>
    /// <remarks>
    /// <para>Only the opening of the file is guarded: failures raised by subsequent operations
    /// on the returned reader surface as their original exceptions.</para>
    /// </remarks>
    public static StreamReader OpenText(string path)
        => Read(path, () => new StreamReader(path));

    /// <summary>
    /// Opens an existing text file for reading with the specified encoding.
    /// </summary>
    /// <param name="path">The file to open for reading.</param>
    /// <param name="encoding">The encoding applied to the contents of the file.</param>
    /// <param name="detectEncodingFromByteOrderMarks">Whether to look for a byte order mark at the beginning
    /// of the file and let it override <paramref name="encoding"/>.</param>
    /// <returns>A <see cref="StreamReader"/> on the specified file.</returns>
    /// <exception cref="BuildFailedException">The file could not be opened.</exception>
    /// <remarks>
    /// <para>Only the opening of the file is guarded: failures raised by subsequent operations
    /// on the returned reader surface as their original exceptions.</para>
    /// </remarks>
    public static StreamReader OpenText(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks = true)
        => Read(path, () => new StreamReader(path, encoding, detectEncodingFromByteOrderMarks));
}
