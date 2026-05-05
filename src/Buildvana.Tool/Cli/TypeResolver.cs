// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using CommunityToolkit.Diagnostics;
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

    public object? Resolve(Type? type) => type is null ? null : _provider.GetService(type);

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
