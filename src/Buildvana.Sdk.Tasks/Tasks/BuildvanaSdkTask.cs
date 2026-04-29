// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Core;
using Buildvana.Sdk.Internal;
using Microsoft.Build.Utilities;

namespace Buildvana.Sdk.Tasks;

public abstract class BuildvanaSdkTask : Task
{
    protected IBuildHost Host => field ??= new MSBuildTaskHost(Log, BuildEngine);

    public sealed override bool Execute()
    {
        try
        {
            _ = Run();
        }
        catch (BuildErrorException ex)
        {
            Log.LogError(ex.Message);
        }
        catch (Exception e) when (!e.IsFatalException())
        {
            Log.LogErrorFromException(e, true, true, GetType().Name + ".cs");
        }

        return !Log.HasLoggedErrors;
    }

    protected abstract Undefined Run();
}
