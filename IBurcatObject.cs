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
using System.Runtime.CompilerServices;
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
        /// Identifies the BDP object revision so another provider knows if its reference is up-to-date; empty (all zeros) for no state.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when trying to set a revision, but the object disallows revisions (empty identifier).</exception>
        Guid Revision { get; set; }

        /// <summary>
        /// Gets the fields of this object to be sent to other providers, allowing the correct setting after construction.
        /// </summary>
        /// <returns>An <see cref="Array"/> of <see cref="BurcatField"/> that contains all fields, and any additional data, to be configured after object creation.</returns>
        BurcatField[] GetBurcatFields();

        /// <summary>
        /// Sets the field of this object to an state represented by the <see cref="BurcatField"/>.
        /// Note: It is expected to only be used by <see cref="BurcatChat"/>, so this method should not modify <see cref="Revision"/>.
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

    public abstract class BurcatObject : IBurcatObject, IComparable<BurcatObject>
    {
        [NotBurcatInvokable]
        private readonly bool canChangeIdentity;
        [NotBurcatInvokable]
        private Guid identifier;
        [NotBurcatInvokable]
        private Guid revision;

        public Guid Identifier { get => identifier; set { if (canChangeIdentity) identifier = value; else throw new InvalidOperationException(); } }
        public Guid Revision { get { if (revision == Guid.AllBitsSet) revision = GuidExtensions.GenerateRandom(); return revision; } set { if (canChangeIdentity) revision = value; else throw new InvalidOperationException(); } }
        
        protected BurcatObject() { identifier = revision = GuidExtensions.GenerateSequential(); canChangeIdentity = true; }
        protected BurcatObject(Guid identifier) { this.identifier = this.revision = identifier; canChangeIdentity = identifier == Guid.Empty; }

        public int CompareTo(BurcatObject? other) => BurcatComparer.Default.Compare(this, other);

        public override bool Equals(object? obj)
        {
            if (obj is BurcatObject other) return BurcatComparer.Default.Equals(this, other);
            else return base.Equals(obj);
        }
        public override int GetHashCode() => BurcatComparer.Default.GetHashCode(this);


        [NotBurcatInvokable]
        protected bool ReviseField<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            else
            {
                field = value;
                if (canChangeIdentity) revision = Guid.AllBitsSet;

                return true;
            }
        }

        public virtual BurcatField[] GetBurcatFields()
        {
            BurcatCache.AddToCache(GetType());
            return BurcatCache.GetFields(this);
        }
        public virtual bool SetBurcatField(BurcatField field)
        {
            BurcatCache.AddToCache(GetType());
            bool updated = BurcatCache.SetField(GetType(), this, field) is null;
            return updated;
        }

        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate(GetBurcatConstructionValues());
        public abstract object?[] GetBurcatConstructionValues();
    }
}
