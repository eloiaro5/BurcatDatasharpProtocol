# BurcatProtocol

BurcatProtocol is the core protocol library for exchanging Burcat objects between
applications. It defines the object model, serialization helpers, message flow,
providers, and stream-based communication primitives used by the Burcat Data
Protocol.

The library targets `.NET 10.0`.

## What It Does

BurcatProtocol lets two sides of a connection exchange objects that implement
`IBurcatObject`. Each supported type has a stable Burcat class identity, and
each object instance can have a `Guid` identifier so it can be referenced,
requested, updated, destroyed, or used as the target of an action.

At a high level, the library provides:

- A common object contract through `IBurcatObject` and `BurcatObject`.
- Stable type identities through `BurcatIdentityAttribute`.
- Object field extraction and mutation through `BurcatField` and `BurcatCache`.
- Conversion between CLR values and protocol objects through `BurcatTranslator`.
- Request, construct, update, destruct, action, send, receive, and purge flows
  through `BurcatChat`.
- Internal and external providers for resolving and storing objects.
- Protocol-aware collections such as `BurcatList<T>`, synchronized lists, sets,
  and dictionaries.
- Stream helpers for identified connections and TCP connection building blocks.

## Core Concepts

### Burcat Objects

Objects sent through the protocol implement `IBurcatObject`. The base
`BurcatObject` class supplies an identifier, equality behavior, field discovery,
field updates, and construction value handling.

```csharp
using BurcatProtocol;

[BurcatIdentity("11111111-1111-1111-1111-111111111111")]
public sealed class UserProfile : BurcatObject
{
    public string Name { get; set; }

    public UserProfile(string name)
    {
        Name = name;
    }

    public override object?[] GetBurcatConstructionValues() => [Name];
}
```

### Class Identities

Types that travel through the protocol need a stable identity. Use
`BurcatIdentityAttribute` on custom Burcat object types, then register accepted
types before communication:

```csharp
BurcatChat.AcceptClass(typeof(UserProfile));
// or scan loaded assemblies:
BurcatChat.AcceptClasses();
```

### Value Translation

`BurcatTranslator` converts supported CLR values into protocol translations.
Default translators include common primitive types, strings, dates, times,
`Guid`, and arrays of many supported primitive types.

Custom translators can be registered when a type needs explicit binary
conversion:

```csharp
BurcatTranslator.Add<MyValue>(
    new Guid("22222222-2222-2222-2222-222222222222"),
    value => value.ToBytes(),
    bytes => MyValue.FromBytes(bytes));
```

### Providers

Providers are responsible for resolving and modifying objects.

- `IInternalProvider` handles local object construction, lookup, creation,
  updates, destruction, and action execution.
- `IExternalProvider` forwards those operations to another source, usually a
  remote stream.
- Built-in providers include `InternalBasicProvider`, `ExternalBasicProvider`,
  collection providers, priority providers, and `NothingProvider`.

Example local setup:

```csharp
using BurcatProtocol;
using BurcatProtocol.Providers;

BurcatChat.InternalProvider = InternalBasicProvider.Instance;
BurcatChat.AcceptClasses();
```

## Communication

`BurcatChat` is the main protocol API. It can send and receive Burcat messages
over an `IdentifiedStream`.

Common operations include:

- `Send` / `SendAsync` for sending an object.
- `Recieve` / `RecieveAsync` for reading and processing the next exchange.
- `SendRequest` / `RelayRequest` for object lookup.
- `SendConstruct` / `RelayConstruct` for object creation.
- `SendUpdate` / `RelayUpdate` for field updates.
- `SendDestruct` / `RelayDestruct` for object destruction.
- `SendAction` / `RelayAction` for invoking protocol-visible actions.
- `Purge` / `PurgeAsync` for advancing a stream to a protocol boundary.

The library also includes TCP connection base classes under
`BurcatProtocol.Connection` for building client and server communication layers.

## Collections

BurcatProtocol includes protocol-aware collection types:

- `BurcatList<T>`
- `ListSet<T>` and `SortedListSet<T>`
- `ListDictionary<TKey, TValue>` and `SortedListDictionary<TKey, TValue>`
- `SynchronizedList`, `SynchronizedSet`, and `SynchronizedDictionary`

These types are useful when collection state needs to be represented as Burcat
objects and synchronized through protocol updates.

## Project Status

BurcatProtocol is under active development. The core protocol shape is present,
but APIs and behavior may still change before the first stable release.

## License

This project is licensed under the terms in `LICENSE`.
