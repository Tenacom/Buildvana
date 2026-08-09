// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using Buildvana.Core.ConsoleOutput;

namespace Buildvana.Core.Testing;

/// <summary>
/// A <see cref="TextWriterReporter"/> that renders to in-memory writers, so that tests can assert on exactly
/// what a reporter would have written without touching the process's console.
/// </summary>
/// <remarks>
/// <para>This is the capture fake for rendered text; <see cref="CaptureReporter"/> is the one for reporting
/// calls. Use this to pin what output looks like, and <see cref="CaptureReporter"/> to check what code under
/// test reports.</para>
/// </remarks>
/// <param name="verbosity">The verbosity that gates which message levels are rendered.</param>
/// <param name="useColor">
/// <see langword="true"/> to wrap colored levels' labels in ANSI escape sequences, <see langword="false"/> to
/// write them verbatim.
/// </param>
public sealed class StringWriterReporter(Verbosity verbosity, bool useColor = false)
    : TextWriterReporter(verbosity, useColor), IDisposable
{
    private readonly StringWriter _output = new(CultureInfo.InvariantCulture);
    private readonly StringWriter _error = new(CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the text written so far to the reporter's output writer.
    /// </summary>
    public string OutputText => _output.ToString();

    /// <summary>
    /// Gets the text written so far to the reporter's error writer.
    /// </summary>
    public string ErrorText => _error.ToString();

    /// <inheritdoc/>
    protected override TextWriter OutputWriter => _output;

    /// <inheritdoc/>
    protected override TextWriter ErrorWriter => _error;

    /// <inheritdoc/>
    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
    }
}
