// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Cake.Core;
using Cake.Frosting;

namespace Buildvana.Tool.Infrastructure;

public sealed class BuildLifetime : FrostingLifetime<BuildContext>
{
    public override void Setup(BuildContext context, ISetupContext info)
    {
    }

    public override void Teardown(BuildContext context, ITeardownContext info)
    {
    }
}
