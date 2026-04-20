// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Infrastructure;
using Cake.Common.Diagnostics;
using Cake.Frosting;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Tasks;

[TaskName(Name)]
[TaskDescription(Description)]
public sealed class DefaultTask : FrostingTask<BuildContext>
{
    private const string Name = "Default";
    private const string Description = "Default task";

    public override void Run(BuildContext context)
    {
        Guard.IsNotNull(context);

        context.Information("The default task does nothing. This is intentional.");
        context.Information("Use `dotnet bv --description` to see the list of available tasks.");
    }
}
