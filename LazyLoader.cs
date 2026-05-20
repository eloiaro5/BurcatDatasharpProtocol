using BurcatProtocol.Annotations;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol
{
    [BurcatIdentity("00000000-0000-0000-0000-79e7141382c2")]
    public sealed class LazyLoader<T> : IBurcatObject where T : IBurcatObject
    {
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        private bool inCache;
        private T value;

        public Guid ClassID { get; } = BurcatChat.GetClassIdentity<T>();
        public Guid ObjectID { get; }
        public bool CanSet { get; }

        public LazyLoader(Guid objectID, bool canSet = default) { ObjectID = objectID; value = default!; CanSet = canSet; }
        public LazyLoader(BurcatIdentifier<T> identifier, bool canSet = default) : this(identifier.Value, canSet) { }

        public async Task<T> GetValueAsync(bool useCache = true, bool ignoreInternal = false, CancellationToken ? token = null)
        {
            if (!inCache || !useCache)
            {
                inCache = useCache;

                T? result = await BurcatChat.RelayRequestAsync<T>(ObjectID, ignoreInternal, token);
                if (result is null) throw new NullReferenceException($"Lazy loaders don't allow nulls, but recieved null when loading."); 
                else value = result;
            }

            return value;
        }
        public T GetValue(bool useCache = true, bool ignoreInternal = false, CancellationToken ? token = null) => GetValueAsync(useCache, ignoreInternal, token).GetAwaiter().GetResult();

        public async Task SetValueAsync(T value, bool useCache = true, bool ignoreInternal = false, CancellationToken? token = null)
        {
            if (CanSet)
            {
                this.value = value;
                inCache = useCache;

                await BurcatChat.RelayConstructAsync(value, ignoreInternal, token);
            }
            else throw new InvalidOperationException("Cannot set a readonly lazy loader.");
        }
        public void SetValue(T value, bool useCache = true, bool ignoreInternal = false, CancellationToken? token = null) => SetValueAsync(value, useCache, ignoreInternal, token).GetAwaiter().GetResult();

        public void Reset() => inCache = false;

        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        bool IBurcatObject.SetBurcatField(BurcatField field) => false;
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.FullObjectsTranslate([new BurcatType(typeof(T), false), ObjectID, CanSet]);

        public static explicit operator LazyLoader<T>(BurcatIdentifier<T> identifier) => new(identifier.Value);
        public static explicit operator LazyLoader<T>(T objectBDP) => new(objectBDP.Identifier);
    }
}
