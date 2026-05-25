using BurcatProtocol.Annotations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Runtime.Serialization;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BurcatProtocol
{
    /// <summary>
    /// Respresents an object of Burcat Data Protocol
    /// </summary>
    public interface IBurcatObject
    {
        /// <summary>
        /// Identifies the BDP object so another provider knows which object references to; empty (all zeros) for no state, disallowing identifier change and sending a new instance every response.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when trying to set an identifier, but the object disallows reference (empty identifier).</exception>
        Guid Identifier { get; set; }

        /// <summary>
        /// Gets the fields of this object to be sent to other providers, allowing the correct setting after construction.
        /// </summary>
        /// <returns>An <see cref="Array"/> of <see cref="BurcatField"/> that contains all fields, and any additional data, to be configured after object creation.</returns>
        BurcatField[] GetBurcatFields();

        /// <summary>
        /// Sets the field of this object to an state represented by the <see cref="BurcatField"/>.
        /// </summary>
        /// <param name="field"><see cref="BurcatField"/> that has the name of the field, or data, to set, and the value it should have.</param>
        /// <returns><see langword="true"/> if the field was set successfully; otherwise, <see langword="false"/>.</returns>
        bool SetBurcatField(BurcatField field);

        /// <summary>
        /// Gets the values used in the object's construction, allowing other providers to construct missing references; it is commonly send after <see cref="GetBurcatFields"/>.
        /// </summary>
        /// <returns>An <see cref="Array"/> of <see cref="object?"/> that contains all values used in the construction.</returns>
        IBurcatObject?[] GetBurcatConstructionValues();
    }

    public abstract class BurcatObject : IBurcatObject
    {
        [NotBurcatInvokable]
        private readonly bool canChangeIdentifier;
        [NotBurcatInvokable]
        private Guid identifier;

        public Guid Identifier { get => identifier; set { if (canChangeIdentifier) identifier = value; else throw new InvalidOperationException(); } }

        protected BurcatObject() { identifier = GuidExtensions.GenerateSequential(); canChangeIdentifier = true; }
        protected BurcatObject(Guid identifier) { this.identifier = identifier; canChangeIdentifier = identifier == Guid.Empty; }

        public override bool Equals(object? obj)
        {
            if (obj is BurcatObject other) return BurcatComparer.Default.Equals(this, other);
            else return base.Equals(obj);
        }
        public override int GetHashCode() => BurcatComparer.Default.GetHashCode(this);

        public virtual BurcatField[] GetBurcatFields()
        {
            BurcatCache.AddToCache(GetType());
            return BurcatCache.GetFields(this);
        }
        public virtual bool SetBurcatField(BurcatField field)
        {
            BurcatCache.AddToCache(GetType());
            return BurcatCache.SetField(GetType(), this, field) is null;
        }
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate(GetBurcatConstructionValues());
        public abstract object?[] GetBurcatConstructionValues();
    }
}
