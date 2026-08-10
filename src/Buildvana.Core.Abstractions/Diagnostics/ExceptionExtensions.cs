// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Security;

namespace Buildvana.Core.Diagnostics;

/// <summary>
/// Provides extension members for <see cref="Exception"/> instances: classification of exceptions
/// as I/O-related, fatal, or critical.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static partial class ExceptionExtensions
{
    extension(Exception @this)
    {
        /// <summary>
        /// <para>Gets a value indicating whether the exception is one the environment raises when it
        /// denies or fails an I/O operation: an <see cref="IOException"/>, an
        /// <see cref="UnauthorizedAccessException"/>, a <see cref="SecurityException"/>, a
        /// <see cref="NotSupportedException"/>, or an <see cref="ArgumentException"/> other than
        /// <see cref="ArgumentNullException"/>.</para>
        /// <para>The last three account for malformed paths, which reach I/O operations through
        /// user-authored input and deserve the same clean failure as a denied access.</para>
        /// </summary>
        /// <remarks>
        /// <para>Several common I/O failure types need no explicit mention because they derive from
        /// <see cref="IOException"/>: <see cref="DirectoryNotFoundException"/>,
        /// <c>DriveNotFoundException</c>, <see cref="EndOfStreamException"/>, <c>FileLoadException</c>,
        /// <see cref="FileNotFoundException"/>, <see cref="PathTooLongException"/>, and
        /// <c>PipeException</c>.</para>
        /// </remarks>
        public bool IsIORelatedException
            => @this is UnauthorizedAccessException
                or NotSupportedException
                or ArgumentException and not ArgumentNullException
                or SecurityException
                or IOException;

        /// <summary>
        /// Gets a value indicating whether the exception is of a type that
        /// will likely cause application failure.
        /// </summary>
        /// <exception cref="NullReferenceException">The exception is <see langword="null"/>.</exception>
        /// <remarks>
        /// <para>Inner exceptions are checked recursively; the value is <see langword="true"/>
        /// if a fatal exception is found at any level of nesting.</para>
        /// <para>The following exception types are considered fatal exceptions:</para>
        /// <list type="bullet">
        /// <item><description><see cref="StackOverflowException"/></description></item>
        /// <item><description><see cref="OutOfMemoryException"/></description></item>
        /// <item><description><see cref="System.Threading.ThreadAbortException"/></description></item>
        /// <item><description><see cref="AccessViolationException"/></description></item>
        /// </list>
        /// </remarks>
        /// <seealso cref="IsCriticalException"/>
        public bool IsFatalException
            => IsFatalExceptionCore(@this)
            || @this.InnerException is { IsFatalException: true }
            || (@this is AggregateException aggregateException && aggregateException.InnerExceptions.Any(static e => e.IsFatalException));

        /// <summary>
        /// <para>Gets a value indicating whether the exception is a critical exception.</para>
        /// <para>Critical exceptions should normally not be caught just to be logged
        /// or ignored, as they are usually signs of a severe problem within a program.</para>
        /// </summary>
        /// <exception cref="NullReferenceException">The exception is <see langword="null"/>.</exception>
        /// <remarks>
        /// <para>Inner exceptions are checked recursively; the value is <see langword="true"/>
        /// if a critical exception is found at any level of nesting.</para>
        /// <para>In addition to all fatal exceptions, as defined by the
        /// <see cref="IsFatalException"/> property, the following exception types are considered
        /// critical exceptions:</para>
        /// <list type="bullet">
        /// <item><description><see cref="AppDomainUnloadedException"/></description></item>
        /// <item><description><see cref="BadImageFormatException"/></description></item>
        /// <item><description><see cref="CannotUnloadAppDomainException"/></description></item>
        /// <item><description><see cref="InvalidProgramException"/></description></item>
        /// <item><description><see cref="NullReferenceException"/></description></item>
        /// </list>
        /// </remarks>
        /// <seealso cref="IsFatalException"/>
        public bool IsCriticalException
            => IsCriticalExceptionCore(@this)
            || @this.InnerException is { IsCriticalException: true }
            || (@this is AggregateException aggregateException && aggregateException.InnerExceptions.Any(static e => e.IsCriticalException));

        /// <summary>
        /// Gets a value indicating whether the exception is either a security exception,
        /// or a critical exception.
        /// </summary>
        /// <exception cref="NullReferenceException">The exception is <see langword="null"/>.</exception>
        /// <remarks>
        /// <para>Inner exceptions are checked recursively; the value is <see langword="true"/>
        /// if a <see cref="SecurityException"/>, or a critical exception as defined by the
        /// <see cref="IsCriticalException"/> property, is found at any level of nesting.</para>
        /// </remarks>
        /// <seealso cref="IsCriticalException"/>
        public bool IsSecurityOrCriticalException
            => IsSecurityOrCriticalExceptionCore(@this)
            || @this.InnerException is { IsSecurityOrCriticalException: true }
            || (@this is AggregateException aggregateException && aggregateException.InnerExceptions.Any(static e => e.IsSecurityOrCriticalException));
    }
}
