// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Adapts <see cref="IServiceCollection"/> to Spectre.Console.Cli's <see cref="ITypeRegistrar"/> contract.
/// </summary>
internal sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _builder;

    public TypeRegistrar(IServiceCollection builder)
    {
        Guard.IsNotNull(builder);
        _builder = builder;
    }

    public void Register(Type service, Type implementation) => _builder.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) => _builder.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory)
    {
        Guard.IsNotNull(factory);
        _builder.AddSingleton(service, _ => factory());
    }

    public ITypeResolver Build() => new TypeResolver(_builder.BuildServiceProvider());
}
