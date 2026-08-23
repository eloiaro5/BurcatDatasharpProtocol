using BurcatProtocol.Annotations;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Lazily resolves and optionally updates a referenced Burcat object while forwarding additional headers.
    /// </summary>
    /// <typeparam name="T">The object type to load.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-79e7141382c2")]
    public sealed class LazyLoader<T> : IBurcatObject where T : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        private T? value;

        /// <summary>
        /// Gets the headers added to provider operations performed by this loader.
        /// </summary>
        public BurcatHeaderSet AdditionalHeaders { get; }

        /// <summary>
        /// Gets the Burcat class identity of the object type to load.
        /// </summary>
        public Guid ClassID { get; } = BurcatChat.GetClassIdentity<T>();

        /// <summary>
        /// Gets the referenced object identifier.
        /// </summary>
        public Guid ObjectID { get; }

        /// <summary>
        /// Gets whether this loader can update the referenced object through the providers.
        /// </summary>
        public bool CanSet { get; }

        /// <summary>
        /// Initializes a lazy loader for an object identifier.
        /// </summary>
        /// <param name="additionalHeaders">The headers to add to provider operations.</param>
        /// <param name="objectID">The referenced object identifier.</param>
        /// <param name="canSet">Whether this loader can update the referenced object.</param>
        public LazyLoader(BurcatHeaderSet additionalHeaders, Guid objectID, bool canSet = default) { AdditionalHeaders = additionalHeaders; ObjectID = objectID; value = default; CanSet = canSet; }

        /// <summary>
        /// Initializes a lazy loader for an object identifier.
        /// </summary>
        /// <param name="objectID">The referenced object identifier.</param>
        /// <param name="canSet">Whether this loader can update the referenced object.</param>
        public LazyLoader(Guid objectID, bool canSet = default) { AdditionalHeaders = []; ObjectID = objectID; value = default; CanSet = canSet; }

        /// <summary>
        /// Initializes a lazy loader for a typed object identifier.
        /// </summary>
        /// <param name="additionalHeaders">The headers to add to provider operations.</param>
        /// <param name="identifier">The typed object identifier.</param>
        /// <param name="canSet">Whether this loader can update the referenced object.</param>
        public LazyLoader(BurcatHeaderSet additionalHeaders, BurcatIdentifier<T> identifier, bool canSet = default) : this(additionalHeaders, identifier.Value, canSet) { }

        /// <summary>
        /// Initializes a lazy loader for a typed object identifier.
        /// </summary>
        /// <param name="identifier">The typed object identifier.</param>
        /// <param name="canSet">Whether this loader can update the referenced object.</param>
        public LazyLoader(BurcatIdentifier<T> identifier, bool canSet = default) : this(identifier.Value, canSet) { }

        /// <summary>
        /// Loads the referenced object asynchronously.
        /// </summary>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The loaded object, or <see langword="null"/> when unavailable.</returns>
        public async Task<T?> GetValueAsync(bool ignoreInternal = false, CancellationToken ? token = null)
        {
            value = await BurcatChat.RelayObjectRequestAsync<T>(new(AdditionalHeaders), ObjectID, ignoreInternal, token);
            return value;
        }

        /// <summary>
        /// Loads the referenced object.
        /// </summary>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The loaded object, or <see langword="null"/> when unavailable.</returns>
        public T? GetValue(bool ignoreInternal = false, CancellationToken ? token = null) => GetValueAsync(ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Updates the referenced object asynchronously through the configured providers.
        /// </summary>
        /// <param name="value">The new object value.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="CanSet"/> is <see langword="false"/>.</exception>
        public async Task SetValueAsync(T value, bool ignoreInternal = false, CancellationToken? token = null)
        {
            if (CanSet)
            {
                this.value = value;
                await BurcatChat.RelayCoupleAsync(new(AdditionalHeaders), value, ignoreInternal, token);
            }
            else throw new InvalidOperationException("Cannot set a readonly lazy loader.");
        }

        /// <summary>
        /// Updates the referenced object through the configured providers.
        /// </summary>
        /// <param name="value">The new object value.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="CanSet"/> is <see langword="false"/>.</exception>
        public void SetValue(T value, bool ignoreInternal = false, CancellationToken? token = null) => SetValueAsync(value, ignoreInternal, token).GetAwaiter().GetResult();

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];

        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([new BurcatType(typeof(T), false), ObjectID, CanSet]);

        /// <summary>
        /// Converts a typed identifier to a lazy loader.
        /// </summary>
        /// <param name="identifier">The typed object identifier.</param>
        public static explicit operator LazyLoader<T>(BurcatIdentifier<T> identifier) => new(identifier.Value);

        /// <summary>
        /// Converts an object to a lazy loader for that object's identifier.
        /// </summary>
        /// <param name="objectBDP">The referenced object.</param>
        public static explicit operator LazyLoader<T>(T objectBDP) => new(objectBDP.Identifier);
    }
}
