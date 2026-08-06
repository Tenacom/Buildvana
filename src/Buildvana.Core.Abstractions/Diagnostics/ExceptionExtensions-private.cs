// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;

namespace Buildvana.Core.Diagnostics;

partial class ExceptionExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFatalExceptionCore(Exception exception)
        => exception is StackOverflowException
            or OutOfMemoryException
            or ThreadAbortException
            or AccessViolationException;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCriticalExceptionCore(Exception exception)
        => IsFatalExceptionCore(exception)
        || exception is AppDomainUnloadedException
        || exception is BadImageFormatException
        || exception is CannotUnloadAppDomainException
        || exception is InvalidProgramException
        || exception is NullReferenceException;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSecurityOrCriticalExceptionCore(Exception exception)
        => exception is SecurityException || IsCriticalExceptionCore(exception);
}
