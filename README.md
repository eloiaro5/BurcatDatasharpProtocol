# BurcatProtocol

BurcatProtocol is the .NET implementation of the Burcat Data Protocol, a
framework communication protocol for sharing object-oriented data between
applications. The protocol is intended to be highly customizable and suitable
for systems where applications need to exchange objects, references, state, and
behavior without depending on the same runtime language.

The library targets `.NET 10.0`.

## What Is The Burcat Data Protocol?

The Burcat Data Protocol reduces object-oriented objects to a common protocol
shape:

- fields, which store object state
- constructors, which define how an object can be created
- actions, which expose behavior that can be requested remotely
- identifiers, which allow an object to be referenced
- revisions, which describe the known state of a referenced object

This makes it possible for one application to send an object to another
application, ask for an object by reference, request the revision, update cache
state, delete cache state, or execute an action on an object or type.

The protocol does not require all behavior to be hard-coded into a transport
layer. Instead, applications define behavior through runtime objects and
providers. Providers interpret received data, decide how references are stored
or resolved, and execute requested actions. This lets application developers
build advanced communication patterns on top of a shared protocol structure.

## Main Features

- Language-neutral object shape through `IBurcatObject`.
- Stable class identities through `BurcatIdentityAttribute`.
- Object references with `Guid` identifiers and revisions.
- Construction-value and field-based object transfer.
- Action execution against instances or types.
- Internal and external providers for storage, lookup, forwarding, and custom
  behavior.
- Runtime reflection cache through `BurcatCache` for faster construction,
  field access, and method invocation.
- CLR value translation through `BurcatTranslator`.
- Stream-based communication through `BurcatChat` and `IdentifiedStream`.
- Failure recovery helpers such as `Purge` / `PurgeAsync`.
- Provider composition for least-resistance-path style routing.
- Protocol-aware collection types for lists, sets, dictionaries, tuples, and
  key/value pairs.

## Core Model

### `IBurcatObject`

Every object that travels through the protocol implements `IBurcatObject`.
The interface defines:

- `Identifier`: the provider reference for the object.
- `Revision`: the known state of that reference.
- `GetBurcatConstructionValues()`: values needed to construct an equivalent
  object.
- `GetBurcatFields()`: fields that must be applied after construction.
- `SetBurcatFields(...)`: field application used by providers during
  construction.

Special identifiers are used by the protocol:

- `Guid.Empty`: no provider reference or no known state.
- `Guid.AllBitsSet`: reserved protocol marker for a null object reference.

Objects whose identifier is `Guid.Empty` are transferred by construction values
and fields because the receiver cannot rely on a provider reference.

### `BurcatObject`

Most application objects should derive from `BurcatObject`. It supplies common
identity and revision behavior, field discovery through `BurcatCache`, and
construction-value translation.

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

When a referenced `BurcatObject` changes through helpers such as revised fields
or protocol-aware collections, its revision can be regenerated so providers can
detect stale references.

## Class Identities

Types that travel through the protocol need stable identities. Use
`BurcatIdentityAttribute` on each protocol-visible object type.

```csharp
[BurcatIdentity("22222222-2222-2222-2222-222222222222")]
public sealed class Message : BurcatObject
{
    public string Text { get; set; }

    public Message(string text)
    {
        Text = text;
    }

    public override object?[] GetBurcatConstructionValues() => [Text];
}
```

Register accepted classes before communication:

```csharp
BurcatChat.AcceptClass(typeof(Message));

// Or scan all loaded assemblies:
BurcatChat.AcceptClasses();
```

`BurcatChat.GetClassIdentity(...)` resolves a CLR type to its Burcat identity,
and `BurcatChat.GetType(...)` resolves an accepted identity back to a CLR type.

## Translating CLR Values

Not every value must implement `IBurcatObject`. `BurcatTranslator` converts
supported CLR values into `BurcatTranslation`, which stores:

- the translated type identity
- the translated byte payload

Default translators include primitive CLR values, strings, dates, times,
`Guid`, and several primitive arrays.

Register defaults at startup if the application needs those built-ins:

```csharp
BurcatTranslator.LoadDefaults();
```

Custom translators can be registered for value types that have a stable binary
representation:

```csharp
BurcatTranslator.Add<MyValue>(
    new Guid("33333333-3333-3333-3333-333333333333"),
    value => value.ToBytes(),
    bytes => MyValue.FromBytes(bytes));
```

Enums can also be registered with the enum helper:

```csharp
BurcatTranslator.Add<MyEnum>(
    new Guid("44444444-4444-4444-4444-444444444444"));
```

## Providers

Providers are the point where protocol data becomes application behavior.

### Internal Providers

`IInternalProvider` handles local operations:

- construct an object
- get the revision
- get an object by reference
- couple an object into cache or storage
- decouple an object from cache or storage
- execute actions

`InternalProvider` provides default construction and action execution through
`BurcatCache`. Concrete providers decide where objects live and how access is
allowed.

Built-in internal providers:

- `InternalBasicProvider`: stores objects in memory.
- `InternalCollectionProvider`: fans operations across multiple internal
  providers.
- `NothingProvider`: no-op provider that resolves nothing and stores nothing.

Example:

```csharp
using BurcatProtocol;
using BurcatProtocol.Providers;

BurcatTranslator.LoadDefaults();
BurcatChat.AcceptClasses();
BurcatChat.InternalProvider = InternalBasicProvider.Instance;
```

### External Providers

`IExternalProvider` forwards operations to another source. In most cases that
source is a remote application connected through an `IdentifiedStream`.

Built-in external providers:

- `ExternalBasicProvider`: forwards every operation through one stream.
- `ExternalCollectionProvider`: broadcasts operations to a provider collection.
- `ExternalPriorityProvider`: queries providers in order and returns the first
  useful result.

This provider model can be used to implement least-resistance-path behavior:
an application can ask several providers for the revision or object and accept
the first provider that can satisfy the request.

## Communication With `BurcatChat`

`BurcatChat` is the main protocol API. It manages:

- accepted classes
- class identity lookup
- object sending
- revision requests
- object requests
- explicit cache coupling and decoupling
- action requests
- stream receive processing
- stream purging after invalid or unexpected data

The protocol works over `IdentifiedStream`, a stream wrapper that carries a
stable stream identifier.

### Sending Objects

```csharp
IdentifiedStream stream = new(networkStream);

Message message = new("Hello from Burcat");
await BurcatChat.SendAsync(stream, message);
```

To send a null value for a type:

```csharp
await BurcatChat.SendAsync<Message>(stream);
```

### Receiving Exchanges

```csharp
ExchangeResult result = await BurcatChat.ReceiveAsync(stream);
```

`ReceiveAsync` processes the next protocol exchange and returns an
`ExchangeResult` describing what was received and what was sent in response.

### Requesting Revisions

Ask configured providers for the revision:

```csharp
Guid revision = await BurcatChat.RelayRevisionRequestAsync(
    BurcatChat.GetClassIdentity<Message>(),
    message.Identifier);
```

Ask a remote stream directly:

```csharp
Guid revision = await BurcatChat.SendRevisionRequestAsync(
    stream,
    BurcatChat.GetClassIdentity<Message>(),
    message.Identifier);
```

### Requesting Objects

Ask configured providers:

```csharp
Message? message = await BurcatChat.RelayObjectRequestAsync<Message>(messageID);
```

Ask a remote stream directly:

```csharp
Message? message = await BurcatChat.SendObjectRequestAsync<Message>(
    stream,
    messageID);
```

### Coupling And Decoupling Cache State

Coupling requests that providers add or update an object in cache or storage:

```csharp
BurcatException? exception = await BurcatChat.RelayCoupleAsync(message);
```

Decoupling requests deletion from cache or storage:

```csharp
BurcatException? exception = await BurcatChat.RelayDecoupleAsync(message);
```

Remote stream variants are available as `SendCoupleAsync` and
`SendDecoupleAsync`.

### Executing Actions

Protocol-visible methods can be invoked as actions. Actions can target an
object instance or a type-level target.

```csharp
ActionResult result = await BurcatChat.RelayActionAsync(
    message,
    "NormalizeText",
    parameters: null);

if (result.SuccessfulExecution)
{
    IBurcatObject? value = result.Value;
}
else
{
    BurcatException? exception = result.Exception;
}
```

Remote stream variants are available as `SendActionAsync`.

## Runtime Cache

`BurcatCache` caches reflected fields, properties, constructors, generic
methods, and methods. It compiles accessors and invokers so repeated protocol
operations do not repeatedly pay the full reflection cost.

The cache is used for:

- extracting object fields
- applying object fields
- constructing objects from protocol constructor values
- invoking actions
- validating field values and action results

Members marked with `NotBurcatInvokableAttribute` are excluded from protocol
field discovery, construction, or action invocation.

Validation uses standard `ValidationAttribute` metadata and
`BurcatCustomValidationAttribute`. This allows objects and actions to enforce
application rules during protocol construction and execution.

## Lazy Loading

`LazyLoader<T>` stores a type and object identifier, then resolves the object
only when requested.

```csharp
LazyLoader<Message> loader = new(message.Identifier);
Message? loaded = await loader.GetValueAsync();
```

If the loader was created with `canSet: true`, it can update the referenced
object through provider coupling:

```csharp
LazyLoader<Message> loader = new(message.Identifier, canSet: true);
await loader.SetValueAsync(updatedMessage);
```

## Protocol-Aware Collections

BurcatProtocol includes collection types that can be represented as Burcat
objects:

- `BurcatList<T>`
- `ListSet<T>`
- `SortedListSet<T>`
- `ListDictionary<TKey, TValue>`
- `SortedListDictionary<TKey, TValue>`
- `BurcatTuple<T1, T2>`
- `KeyValueDuo<TKey, TValue>`

These collections are useful when collection state must move through the
protocol or participate in object construction. Referenced collections update
their revision when mutated, if applicable.

Example:

```csharp
BurcatList<string> tags = new(["protocol", "objects"]);
tags.Add("providers");
```

## TCP Helpers

The `BurcatProtocol.Connection` namespace includes base classes for TCP
connections:

- `ClientConnectionTCP`
- `ServerConnectionTCP`

These classes establish SSL-authenticated streams and leave certificate and SSL
policy details to derived classes through `SslClientAuthenticationOptions` and
`SslServerAuthenticationOptions`.

The namespace also contains command parsing utilities:

- `QuestionProcessor`
- `QuestionEnumerator`
- `QuestionArgumentKey`

## Failure Recovery

When invalid or unexpected data is received, `BurcatChat.PurgeAsync` can advance
the stream until the next known protocol ending marker. This helps recover from
fault data without necessarily tearing down the whole connection.

```csharp
await BurcatChat.PurgeAsync(stream);
```

## Extension Points

BurcatProtocol is designed around extension points:

- implement `IBurcatObject` or derive from `BurcatObject` for protocol objects
- add `BurcatIdentityAttribute` for stable class identities
- register CLR value translators with `BurcatTranslator`
- implement `IInternalProvider` for custom storage, permissions, validation, or
  action behavior
- implement `IExternalProvider` for custom routing, forwarding, or remote
  resolution
- compose providers to model priority, fallback, cache layers, federation, or
  least-resistance-path routing

Using a shared object structure and provider-defined behavior, applications can
build complex distributed ecosystems where references, state, and behavior are
communicated through a common protocol.

## Startup Checklist

Typical application setup:

```csharp
using BurcatProtocol;
using BurcatProtocol.Providers;

BurcatTranslator.LoadDefaults();
BurcatChat.AcceptClasses();
BurcatChat.InternalProvider = InternalBasicProvider.Instance;
```

For remote communication, wrap the stream and configure an external provider:

```csharp
IdentifiedStream stream = new(networkStream);
BurcatChat.ExternalProvider = new ExternalBasicProvider(stream);
```

## Project Status

BurcatProtocol is under active development. The core protocol shape is present,
but APIs and behavior may still change before a stable release.

## License

This project is licensed under the terms in `LICENSE`.
