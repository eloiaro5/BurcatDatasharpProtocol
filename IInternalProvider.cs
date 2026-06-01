using BurcatProtocol.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace BurcatProtocol
{
    public interface IInternalProvider
    {
        IBurcatObject? ConstructObject(Guid? streamID, Type objectType, Guid objectID, Guid revisionID, IBurcatObject?[] parameters, BurcatField[] fields);

        Guid GetRevision(Guid? streamID, Type objectType, Guid objectID);
        IBurcatObject? GetObject(Guid? streamID, Type objectType, Guid objectID);

        BurcatException? CoupleCache(Guid? streamID, IBurcatObject objectBDP, bool explicitelyRequested);
        BurcatException? DecoupleCache(Guid? streamID, IBurcatObject objectBDP);

        ActionResult ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters);
    }

    public abstract class InternalProvider : IInternalProvider
    {
        public virtual IBurcatObject? ConstructObject(Guid? streamID, Type objectType, Guid objectID, Guid revisionID, IBurcatObject?[] parameters, BurcatField[] fields)
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
                BurcatCache.AddToCache(objectType);
                foreach (BurcatField field in fields) reference.SetBurcatField(field);

                if (reference.Identifier != Guid.Empty)
                {
                    reference.Identifier = objectID;
                    reference.Revision = revisionID;
                }
            }
            return reference;
        }

        public virtual ActionResult ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters)
        {
            BurcatCache.AddToCache(objectType);
            return BurcatCache.ExecuteAction(objectType, objectBDP, action, BurcatTranslator.ObjectsTranslate(parameters));
        }

        public abstract Guid GetRevision(Guid? streamID, Type objectType, Guid objectID);
        public abstract IBurcatObject? GetObject(Guid? streamID, Type objectType, Guid objectID);

        public abstract BurcatException? CoupleCache(Guid? streamID, IBurcatObject objectBDP, bool explicitelyRequested);
        public abstract BurcatException? DecoupleCache(Guid? streamID, IBurcatObject objectBDP);
    }
}
