# Dependency injection

This file is the same in every repository that copies `.claude`. A fact specific to one repository lives in another rule file.

## Disposability is an implementation detail

Disposability belongs to an implementation, not to the contract its consumers use. A service that holds a repository handle, an `HttpClient`, or a file is disposable because of how it works, not because of what it offers.

- A service contract never extends `IDisposable` or `IAsyncDisposable`.
- Register a disposable implementation under its contract: `AddSingleton<IFooService, FooService>()`. The service provider disposes every instance it creates, according to the type of the instance, not the type it is registered under.
- A consumer depends on the contract, never on a disposable implementation.
- A consumer never disposes an injected service. The service provider owns the lifetime of the service, and another consumer may still use it.
- Do not suppress CA2213 on a field that holds an injected service. The warning means that the field has the type of an implementation. Change the type of the field to the contract instead.

A consumer that holds a concrete disposable service looks like its owner, to an analyzer and to a reader. CA2213 reports it only when the consumer is itself disposable, so the defect can stay silent until then.

## When a service needs a contract

A contract is an interface, or an abstract base class.

- A service that a packaged library exposes gets a contract. Its consumers cannot know which implementation the service provider will hold.
- A service whose implementation is not sealed gets a contract, for the same reason.
- An internal, sealed service gets a contract when its implementation is disposable.
- Any other internal, sealed service may be injected as its class. Do not add an interface only in case a future implementation turns out to be disposable. Add it when that implementation arrives, and switch the consumers in the same change.
