// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services;

/// <summary>
/// Holds MSBuild properties forwarded as <c>-p:Key=Value</c> on every <c>dotnet</c> invocation.
/// </summary>
public sealed class MSBuildProperties
{
    private readonly Dictionary<string, string> _properties = new(StringComparer.Ordinal);

    /// <summary>
    /// Sets the value of an MSBuild property.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The property value.</param>
    public void Set(string name, string value)
    {
        Guard.IsNotNullOrEmpty(name);
        Guard.IsNotNull(value);
        _properties[name] = value;
    }

    /// <summary>
    /// Enumerates the configured properties as MSBuild command-line arguments (<c>-p:Key=Value</c>).
    /// </summary>
    /// <returns>A sequence of MSBuild command-line arguments.</returns>
    public IEnumerable<string> EnumerateAsArgs() => _properties.Select(kvp => $"-p:{kvp.Key}={kvp.Value}");
}
