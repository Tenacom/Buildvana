// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;
using Buildvana.Sdk;
using Buildvana.Sdk.Tasks;

/// <summary>
/// A do-nothing <see cref="BuildvanaSdkTask"/> that exposes the protected
/// <see cref="BuildvanaSdkTask.Reporter"/> property to tests.
/// </summary>
internal sealed class ReporterProbeTask : BuildvanaSdkTask
{
    public IReporter ExposedReporter => Reporter;

    protected override Undefined Run() => Undefined.Value;
}
