// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using Buildvana.Core;
using Cake.Core;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services;

public sealed partial class OptionsService
{
    // Regular expressions used by OptionNameToEnvironmentVariableName
    private static readonly Regex UnderscoreCasingRegex1 = GetUnderscoreCasingRegex1();
    private static readonly Regex UnderscoreCasingRegex2 = GetUnderscoreCasingRegex2();
    private static readonly Regex UnderscoreCasingRegex3 = GetUnderscoreCasingRegex3();

    private readonly ICakeContext _context;
    private readonly Dictionary<string, string> _options = [];

    public OptionsService(ICakeContext context)
    {
        Guard.IsNotNull(context);
        _context = context;
    }

    /// <summary>
    /// Sets an option. The value set by this method will take precedence over arguments and environment variables.
    /// </summary>
    /// <param name="name">The option name.</param>
    /// <param name="value">The option value.</param>
    public void SetOption(string name, string value)
    {
        Guard.IsNotNullOrEmpty(name);
        Guard.IsNotNullOrEmpty(value);
        _options[name] = value;
    }

    /// <summary>
    /// Tells whether the specified option is present, either as an explicitly set option, as an argument, or as an environment variable.
    /// </summary>
    /// <param name="name">The option name.</param>
    /// <returns>
    /// <para>If an option with the specified name has been explicitly set, <see langword="true"/>.</para>
    /// <para>If an argument with the specified name is present, <see langword="true"/>.</para>
    /// <para>If an environment variable with the specified name (converted to UNDERSCORE_UPPER_CASE) is present, <see langword="true"/>.</para>
    /// <para>Otherwise, <see langword="false"/>.</para>
    /// </returns>
    public bool HasOption(string name) => TryGetOptionString(name, out _);

    /// <summary>
    /// <para>Gets the value of an option from, in this order:</para>
    /// <list type="bullet">
    /// <item><description>options set via <see cref="SetOption"/>, or</description></item>
    /// <item><description>a command line argument with the specified name, or</description></item>
    /// <item><description>an environment variable with the specified name converted to UNDERSCORE_UPPER_CASE, or</description></item>
    /// <item><description>the provided default value.</description></item>
    /// </list>
    /// </summary>
    /// <typeparam name="T">The type of the option value.</typeparam>
    /// <param name="name">The option name.</param>
    /// <param name="defaultValue">The value returned if the option was was found.</param>
    /// <returns>The value of the option, converted to <typeparamref name="T" />.</returns>
    public T GetOption<T>(string name, T defaultValue)
        where T : notnull
        => TryGetOptionString(name, out var stringValue)
            ? ConvertOptionValue<T>(stringValue)
            : defaultValue;

    /// <summary>
    /// <para>Gets the value of an option from, in this order:</para>
    /// <list type="bullet">
    /// <item><description>options set via <see cref="SetOption"/>, or</description></item>
    /// <item><description>a command line argument with the specified name, or</description></item>
    /// <item><description>an environment variable with the specified name converted to UNDERSCORE_UPPER_CASE.</description></item>
    /// </list>
    /// <para>If the option is not found, this method fails the build.</para>
    /// </summary>
    /// <typeparam name="T">The type of the option value.</typeparam>
    /// <param name="name">The option name.</param>
    /// <returns>The value of the option, converted to <typeparamref name="T" />.</returns>
    /// <exception cref="BuildFailedException">The specified option was not found.</exception>
    public T GetOptionOrFail<T>(string name)
        where T : notnull
        => TryGetOptionString(name, out var stringValue)
            ? ConvertOptionValue<T>(stringValue)
            : throw new BuildFailedException($"Option {name} / environment variable {OptionNameToEnvironmentVariableName(name)} not found or empty.");

    [GeneratedRegex("([A-Z]+)([A-Z][a-z])", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex GetUnderscoreCasingRegex1();

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex GetUnderscoreCasingRegex2();

    [GeneratedRegex(@"[-\s]", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex GetUnderscoreCasingRegex3();

    private static string OptionNameToEnvironmentVariableName(string name)
    {
        // minorVersionOfSQLServer2012SomeMoreWords-more stuff
        name = UnderscoreCasingRegex1.Replace(name, "$1_$2"); // -> minorVersionOfSQL_Server2012SomeMoreWords-more stuff
        name = UnderscoreCasingRegex2.Replace(name, "$1_$2"); // -> minor_Version_Of_SQL_Server2012_Some_More_Words-more_stuff
        name = UnderscoreCasingRegex3.Replace(name, "_");     // -> minor_Version_Of_SQL_Server2012_Some_More_Words_more_stuff
        return name.ToUpperInvariant();                       // -> MINOR_VERSION_OF_SQL_SERVER2012_SOME_MORE_WORDS_MORE_STUFF
    }

    private static T ConvertOptionValue<T>(string value)
        where T : notnull
    {
        var converter = TypeDescriptor.GetConverter(typeof(T));
        return (T)converter.ConvertFromInvariantString(value)!;
    }

    private bool TryGetOptionString(string name, [MaybeNullWhen(false)] out string value)
    {
        Guard.IsNotNullOrEmpty(name);
        if (_options.TryGetValue(name, out value))
        {
            return true;
        }

        value = _context.Arguments.GetArguments(name)?.FirstOrDefault();
        if (!string.IsNullOrEmpty(value))
        {
            _options[name] = value;
            return true;
        }

        value = _context.Environment.GetEnvironmentVariable(OptionNameToEnvironmentVariableName(name));
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        _options[name] = value;
        return true;
    }
}
