using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    /// <summary>
    /// Internal provider that advertises the default capabilities but intentionally stores and resolves no objects.
    /// </summary>
    public sealed class NothingProvider : InternalProvider
    {
        /// <summary>
        /// Gets the shared no-op provider instance.
        /// </summary>
        public static NothingProvider Instance { get; } = new();

        /// <inheritdoc/>
        public override Guid GetRevision(BurcatHead head, Type objectType, Guid objectID) => Guid.Empty;

        /// <inheritdoc/>
        public override IBurcatObject? GetObject(BurcatHead head, Type objectType, Guid objectID) => null;

        /// <inheritdoc/>
        public override BurcatException? CoupleCache(BurcatHead head, IBurcatObject objectBDP, bool explicitelyRequested) => new($"A {nameof(NothingProvider)} is not able to maintain objects.");

        /// <inheritdoc/>
        public override BurcatException? DecoupleCache(BurcatHead head, IBurcatObject objectBDP) => new($"A {nameof(NothingProvider)} is not able to maintain objects.");
    }
}
