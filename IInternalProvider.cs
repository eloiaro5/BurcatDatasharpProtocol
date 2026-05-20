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
        IBurcatObject? ConstructObject(Guid? streamID, Type objectType, Guid objectID, IBurcatObject?[] parameters, BurcatField[] fields);

        IBurcatObject? GetObject(Guid? streamID, Type objectType, Guid objectID);
        BurcatException? CreateObject(Guid? streamID, IBurcatObject objectBDP);
        BurcatException? UpdateObject(Guid? streamID, Type objectType, Guid? objectID, BurcatField field);
        BurcatException? DestroyObject(Guid? streamID, Type objectType, Guid objectID);

        ActionResult ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, IBurcatObject?[] parameters);
    }

    public abstract class InternalProvider : IInternalProvider
    {
        public virtual IBurcatObject? ConstructObject(Guid? streamID, Type objectType, Guid objectID, IBurcatObject?[] parameters, BurcatField[] fields)
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
                if (reference.Identifier != Guid.Empty) reference.Identifier = objectID;
            }
            return reference;
        }

        public virtual BurcatException? UpdateObject(Guid? streamID, Type objectType, Guid? objectID, BurcatField field)
        {
            IBurcatObject? objectBDP = objectID is Guid oID ? GetObject(streamID, objectType, oID) : null;

            BurcatCache.AddToCache(objectType);
            return BurcatCache.SetField(objectType, objectBDP, field, true);
        }

        public virtual ActionResult ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, IBurcatObject?[] parameters)
        {
            BurcatCache.AddToCache(objectType);
            return BurcatCache.ExecuteAction(objectType, objectBDP, action, parameters);
        }

        public abstract IBurcatObject? GetObject(Guid? streamID, Type objectType, Guid objectID);

        public abstract BurcatException? CreateObject(Guid? streamID, IBurcatObject objectBDP);
        public abstract BurcatException? DestroyObject(Guid? streamID, Type objectType, Guid objectID);
    }
}
