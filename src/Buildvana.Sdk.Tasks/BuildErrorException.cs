// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Buildvana.Sdk;

[Serializable]
public sealed class BuildErrorException : Exception
{
    public BuildErrorException()
    {
    }

    public BuildErrorException(string message)
        : base(message)
    {
    }

    public BuildErrorException(string format, params object[] args)
        : base(string.Format(CultureInfo.InvariantCulture, format, args))
    {
    }

    public BuildErrorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    [DoesNotReturn]
    public static void ThrowNew(string message)
        => throw new BuildErrorException(message);

    [DoesNotReturn]
    public static T ThrowNew<T>(string message)
        => throw new BuildErrorException(message);

    [DoesNotReturn]
    public static void ThrowNew(string format, params object[] args)
        => throw new BuildErrorException(format, args);

    [DoesNotReturn]
    public static T ThrowNew<T>(string format, params object[] args)
        => throw new BuildErrorException(format, args);
}
