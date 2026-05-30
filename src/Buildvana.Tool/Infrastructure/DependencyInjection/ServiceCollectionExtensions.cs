// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to add Buildvana Tool services.
/// </summary>
internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection @this)
    {
        /// <summary>
        /// Adds support for resolving <see cref="Lazy{T}"/> instances.
        /// </summary>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddLazySupport() => @this.AddTransient(typeof(Lazy<>), typeof(LazyResolver<>));
    }
}
