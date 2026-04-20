// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Infrastructure;
using Cake.Frosting;

namespace Buildvana.Tool;

internal static class Program
{
    public static int Main(string[] args)
    {
        var cakeArgs = CommandLineParser.Parse(args);
        return cakeArgs is null ? 0 : new CakeHost()
            .UseStartup<BuildStartup>()
            .UseContext<BuildContext>()
            .UseLifetime<BuildLifetime>()
            .Run(cakeArgs);
    }
}
