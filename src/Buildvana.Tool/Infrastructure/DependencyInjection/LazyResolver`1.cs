// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Infrastructure.DependencyInjection;

internal sealed class LazyResolver<T> : Lazy<T>
    where T : notnull
{
    public LazyResolver(IServiceProvider provider)
        : base(provider.GetRequiredService<T>, LazyThreadSafetyMode.ExecutionAndPublication)
    {
    }
}
