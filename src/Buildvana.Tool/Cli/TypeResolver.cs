// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Adapts <see cref="IServiceProvider"/> to Spectre.Console.Cli's <see cref="ITypeResolver"/> contract.
/// </summary>
internal sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        Guard.IsNotNull(provider);
        _provider = provider;
    }

    public object? Resolve(Type? type)
    {
        if (type is null)
        {
            return null;
        }

        // Spectre's TypeResolverAdapter falls back to Activator.CreateInstance(type) when we return null,
        // which throws on interface types such as IEnumerable<T> (Spectre requests this shape for its
        // IHelpProvider resolution). Resolve generic IEnumerable<T> via GetServices(elementType) so
        // M.E.DI returns the registered set instead of null.
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return _provider.GetServices(type.GetGenericArguments()[0]);
        }

        return _provider.GetService(type);
    }

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
