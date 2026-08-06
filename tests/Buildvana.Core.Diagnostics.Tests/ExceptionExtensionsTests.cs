// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security;
using Buildvana.Core.Diagnostics;

#pragma warning disable CA2201 // Do not raise reserved exception types — the classifier under test is defined over these very types; they are instantiated as specimens, never thrown
internal sealed class ExceptionExtensionsTests
{
    [Test]
    public async Task IsIORelatedException_IsTrueForEnvironmentDrivenIOFailures()
    {
        Exception[] exceptions =
        [
            new IOException(),
            new FileNotFoundException(),
            new DirectoryNotFoundException(),
            new UnauthorizedAccessException(),
            new SecurityException(),
            new NotSupportedException(),
            new ArgumentException(),
            new ArgumentOutOfRangeException(),
        ];

        await Assert.That(Misclassified(exceptions, static e => e.IsIORelatedException, expected: true)).IsEmpty();
    }

    [Test]
    public async Task IsIORelatedException_IsFalseForProgrammerErrorsAndUnrelatedFailures()
    {
        Exception[] exceptions = [new ArgumentNullException(), new InvalidOperationException(), new Exception()];

        await Assert.That(Misclassified(exceptions, static e => e.IsIORelatedException, expected: false)).IsEmpty();
    }

    [Test]
    public async Task IsFatalException_IsTrueForFatalExceptions()
    {
        Exception[] exceptions = [new StackOverflowException(), new OutOfMemoryException(), new AccessViolationException()];

        await Assert.That(Misclassified(exceptions, static e => e.IsFatalException, expected: true)).IsEmpty();
    }

    [Test]
    public async Task IsFatalException_IsFalseForNonFatalExceptions()
    {
        // NullReferenceException is critical, but not fatal.
        Exception[] exceptions = [new Exception(), new IOException(), new NullReferenceException()];

        await Assert.That(Misclassified(exceptions, static e => e.IsFatalException, expected: false)).IsEmpty();
    }

    [Test]
    public async Task IsFatalException_ChecksInnerExceptionsRecursively()
    {
        var exception = new Exception("outer", new InvalidOperationException("mid", new OutOfMemoryException()));

        await Assert.That(exception.IsFatalException).IsTrue();
    }

    [Test]
    public async Task IsFatalException_ChecksAggregateInnerExceptions()
    {
        var aggregate = new AggregateException(new InvalidOperationException(), new OutOfMemoryException());

        await Assert.That(aggregate.IsFatalException).IsTrue();
    }

    [Test]
    public async Task IsFatalException_IsFalseForAggregateOfNonFatalExceptions()
    {
        var aggregate = new AggregateException(new InvalidOperationException(), new IOException());

        await Assert.That(aggregate.IsFatalException).IsFalse();
    }

    [Test]
    public async Task IsCriticalException_IsTrueForCriticalExceptions()
    {
        Exception[] exceptions =
        [
            new AppDomainUnloadedException(),
            new BadImageFormatException(),
            new CannotUnloadAppDomainException(),
            new InvalidProgramException(),
            new NullReferenceException(),
            new OutOfMemoryException(), // Fatal implies critical.
        ];

        await Assert.That(Misclassified(exceptions, static e => e.IsCriticalException, expected: true)).IsEmpty();
    }

    [Test]
    public async Task IsCriticalException_IsFalseForNonCriticalExceptions()
    {
        Exception[] exceptions = [new Exception(), new IOException(), new SecurityException(), new ArgumentException()];

        await Assert.That(Misclassified(exceptions, static e => e.IsCriticalException, expected: false)).IsEmpty();
    }

    [Test]
    public async Task IsCriticalException_ChecksInnerExceptionsRecursively()
    {
        var exception = new Exception("outer", new NullReferenceException());

        await Assert.That(exception.IsCriticalException).IsTrue();
    }

    [Test]
    public async Task IsCriticalException_ChecksAggregateInnerExceptions()
    {
        var aggregate = new AggregateException(new IOException(), new NullReferenceException());

        await Assert.That(aggregate.IsCriticalException).IsTrue();
    }

    [Test]
    public async Task IsSecurityOrCriticalException_IsTrueForSecurityAndCriticalExceptions()
    {
        Exception[] exceptions = [new SecurityException(), new NullReferenceException(), new OutOfMemoryException()];

        await Assert.That(Misclassified(exceptions, static e => e.IsSecurityOrCriticalException, expected: true)).IsEmpty();
    }

    [Test]
    public async Task IsSecurityOrCriticalException_IsFalseForOtherExceptions()
    {
        Exception[] exceptions = [new Exception(), new IOException(), new ArgumentException()];

        await Assert.That(Misclassified(exceptions, static e => e.IsSecurityOrCriticalException, expected: false)).IsEmpty();
    }

    [Test]
    public async Task IsSecurityOrCriticalException_ChecksInnerExceptionsRecursively()
    {
        var exception = new Exception("outer", new SecurityException());

        await Assert.That(exception.IsSecurityOrCriticalException).IsTrue();
    }

    [Test]
    public async Task IsSecurityOrCriticalException_ChecksAggregateInnerExceptions()
    {
        var aggregate = new AggregateException(new IOException(), new SecurityException());

        await Assert.That(aggregate.IsSecurityOrCriticalException).IsTrue();
    }

    // The type names of the exceptions the classifier got wrong, so a failed test names the culprits.
    private static string[] Misclassified(Exception[] exceptions, Func<Exception, bool> classifier, bool expected)
        => [.. exceptions.Where(e => classifier(e) != expected).Select(static e => e.GetType().Name)];
}
