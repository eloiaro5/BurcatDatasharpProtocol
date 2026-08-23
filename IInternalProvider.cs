using BurcatProtocol.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace BurcatProtocol
{
    /// <summary>
    /// Manages <see cref="IBurcatObject"/> instances inside the current process or storage boundary.
    /// </summary>
    /// <remarks>
    /// Internal providers advertise identities and headers per stream, and use each
    /// operation's exchanged <see cref="BurcatHead"/> to authorize or contextualize
    /// construction, lookup, persistence, and action execution.
    /// </remarks>
    public interface IInternalProvider
    {
        /// <summary>
        /// Gets the Burcat identities this provider supports on an identified stream.
        /// </summary>
        /// <param name="streamID">The identity of the stream for which capabilities are requested.</param>
        /// <returns>The identities supported by the provider.</returns>
        BurcatIdentitySet GetIdentities(Guid streamID);

        /// <summary>
        /// Gets the headers this provider supports on an identified stream.
        /// </summary>
        /// <param name="streamID">The identity of the stream for which capabilities are requested.</param>
        /// <returns>The headers supported by the provider.</returns>
        BurcatHeaderSet GetHeaders(Guid streamID);

        /// <summary>
        /// Constructs a Burcat object, or returns <see langword="null"/> for a null object value.
        /// </summary>
        /// <param name="head">
        /// The negotiated headers and stream identity associated with the request.
        /// </param>
        /// <param name="objectType">The CLR type to construct.</param>
        /// <param name="objectID">The provider reference to assign to the constructed object.</param>
        /// <param name="revisionID">The revision to assign to the constructed object.</param>
        /// <param name="parameters">The protocol values used as constructor arguments.</param>
        /// <param name="fields">The protocol fields to apply after construction.</param>
        /// <returns>The constructed nullable object, or <see langword="null"/> when no object can be constructed.</returns>
        IBurcatObject? ConstructObject(BurcatHead head, Type objectType, Guid objectID, Guid revisionID, IBurcatObject?[] parameters, BurcatField[] fields);

        /// <summary>
        /// Gets the revision of an object reference.
        /// </summary>
        /// <param name="head">The negotiated headers and stream identity associated with the request.</param>
        /// <param name="objectType">The CLR type of the referenced object.</param>
        /// <param name="objectID">The provider reference of the requested object.</param>
        /// <returns>
        /// The object's current revision, or <see cref="Guid.Empty"/> when the reference
        /// is unknown or has no available revision.
        /// </returns>
        Guid GetRevision(BurcatHead head, Type objectType, Guid objectID);

        /// <summary>
        /// Gets the latest available object for a reference.
        /// </summary>
        /// <param name="head">The negotiated headers and stream identity associated with the request.</param>
        /// <param name="objectType">The CLR type of the referenced object.</param>
        /// <param name="objectID">The provider reference of the requested object.</param>
        /// <returns>The referenced object, or <see langword="null"/> when it is not available.</returns>
        IBurcatObject? GetObject(BurcatHead head, Type objectType, Guid objectID);

        /// <summary>
        /// Tries to add or update an object in the provider's cache or backing store.
        /// </summary>
        /// <param name="head">The negotiated headers and stream identity associated with the request.</param>
        /// <param name="objectBDP">The object to add or update.</param>
        /// <param name="explicitelyRequested">
        /// <see langword="true"/> when the operation was explicitly requested by the caller;
        /// <see langword="false"/> when it is part of protocol synchronization.
        /// </param>
        /// <returns><see langword="null"/> on success; otherwise, the protocol exception describing the failure.</returns>
        BurcatException? CoupleCache(BurcatHead head, IBurcatObject objectBDP, bool explicitelyRequested);

        /// <summary>
        /// Tries to delete an object from the provider's cache or backing store.
        /// </summary>
        /// <param name="head">The negotiated headers and stream identity associated with the request.</param>
        /// <param name="objectBDP">The object to remove.</param>
        /// <returns><see langword="null"/> on success; otherwise, the protocol exception describing the failure.</returns>
        BurcatException? DecoupleCache(BurcatHead head, IBurcatObject objectBDP);

        /// <summary>
        /// Executes an action on an object or type and returns the action result.
        /// </summary>
        /// <param name="head">The negotiated headers and stream identity associated with the request.</param>
        /// <param name="objectType">The CLR type that declares or receives the action.</param>
        /// <param name="objectBDP">
        /// The target object for instance actions, or <see langword="null"/> for static or type-level actions.
        /// </param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The CLR parameter values to translate and pass to the action.</param>
        /// <returns>The result of the action, including any exception produced by its execution.</returns>
        ActionResult ExecuteAction(BurcatHead head, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters);
    }

    /// <summary>
    /// Provides default construction and action execution behavior for internal providers.
    /// </summary>
    /// <remarks>
    /// Derived providers supply object lookup and persistence behavior while this base
    /// class handles generic Burcat type parameters, constructor invocation, field
    /// application, identity assignment, and action dispatch through <see cref="BurcatCache"/>.
    /// </remarks>
    public abstract class InternalProvider : IInternalProvider
    {
        /// <inheritdoc/>
        public virtual BurcatIdentitySet GetIdentities(Guid streamID) => BurcatChat.AcceptedIdentities;

        /// <inheritdoc/>
        public virtual BurcatHeaderSet GetHeaders(Guid streamID) => [];

        /// <inheritdoc/>
        public virtual IBurcatObject? ConstructObject(BurcatHead head, Type objectType, Guid objectID, Guid revisionID, IBurcatObject?[] parameters, BurcatField[] fields)
        {
            LinkedList<Type> genericTypes = [];
            for (int i = 0; i < parameters.Length && parameters[i] is BurcatType type; i++) genericTypes.AddLast(type.Nullable ? type.GetTypeCLR().MakeNullable() : type.GetTypeCLR());
            if (genericTypes.Count != 0)
            {
                IBurcatObject?[] tmp = parameters;
                parameters = new IBurcatObject?[tmp.Length - genericTypes.Count];
                Array.Copy(tmp, genericTypes.Count, parameters, 0, parameters.Length);

                objectType = objectType.MakeGenericType([.. genericTypes]);
            }

            BurcatCache.AddToCache(objectType);
            IBurcatObject? reference = BurcatCache.Construct(objectType, parameters);
            if (reference is not null)
            {
                reference.SetBurcatFields(fields);

                if (reference.Identifier != Guid.Empty)
                {
                    reference.Identifier = objectID;
                    reference.Revision = revisionID;
                }
            }
            return reference;
        }

        /// <inheritdoc/>
        public virtual ActionResult ExecuteAction(BurcatHead head, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters)
        {
            BurcatCache.AddToCache(objectType);
            return BurcatCache.ExecuteAction(objectType, objectBDP, action, BurcatTranslator.ObjectsTranslate(parameters));
        }

        /// <inheritdoc/>
        public abstract Guid GetRevision(BurcatHead head, Type objectType, Guid objectID);

        /// <inheritdoc/>
        public abstract IBurcatObject? GetObject(BurcatHead head, Type objectType, Guid objectID);

        /// <inheritdoc/>
        public abstract BurcatException? CoupleCache(BurcatHead head, IBurcatObject objectBDP, bool explicitelyRequested);

        /// <inheritdoc/>
        public abstract BurcatException? DecoupleCache(BurcatHead head, IBurcatObject objectBDP);
    }
}
