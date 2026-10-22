// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.Services.Platforms;

/// <summary>
/// Base class for the writers of named step outputs, which later steps of a CI job read.
/// </summary>
/// <remarks>
/// <para>The medium and the format belong to the platform: a file, a command on standard output, or anything
/// else the platform reads. <see cref="CIPlatform.OpenStepOutputs"/> returns the writer of its own platform.</para>
/// </remarks>
internal abstract class StepOutputWriter
{
    private protected StepOutputWriter()
    {
    }

    /// <summary>
    /// Writes a step output.
    /// </summary>
    /// <param name="name">The name of the output.</param>
    /// <param name="value">The value of the output.</param>
    /// <exception cref="ArgumentException">The platform cannot carry <paramref name="name"/> or <paramref name="value"/>.</exception>
    /// <exception cref="Core.BuildFailedException">The output could not be written.</exception>
    public abstract void Write(string name, string value);
}
