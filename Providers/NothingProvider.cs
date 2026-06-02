using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    /// <summary>
    /// Internal provider that intentionally stores nothing and resolves no objects.
    /// </summary>
    public sealed class NothingProvider : InternalProvider
    {
        /// <summary>
        /// Gets the shared no-op provider instance.
        /// </summary>
        public static NothingProvider Instance { get; } = new();

        /// <inheritdoc/>
        public override Guid GetRevision(Guid? streamID, Type objectType, Guid objectID) => Guid.Empty;

        /// <inheritdoc/>
        public override IBurcatObject? GetObject(Guid? streamID, Type objectType, Guid objectID) => null;

        /// <inheritdoc/>
        public override BurcatException? CoupleCache(Guid? streamID, IBurcatObject objectBDP, bool explicitelyRequested) => new($"A {nameof(NothingProvider)} is not able to maintain objects.");

        /// <inheritdoc/>
        public override BurcatException? DecoupleCache(Guid? streamID, IBurcatObject objectBDP) => new($"A {nameof(NothingProvider)} is not able to maintain objects.");
    }
}
