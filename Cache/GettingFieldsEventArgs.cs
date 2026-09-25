using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace BurcatProtocol.Cache
{
    public class BeforeGettingFieldsEventArgs(IBurcatObject requestedObject)
    {
        public IBurcatObject RequestedObject { get; } = requestedObject;
    }

    public class AfterGettingFieldsEventArgs(IBurcatObject requestedObject, MemberInfo[] inspectedMembers, BurcatField[] obtainedFields)
    {
        public IBurcatObject RequestedObject { get; } = requestedObject;
        public MemberInfo[] InspectedMembers { get; } = inspectedMembers;

        public BurcatField[] ObtainedFields { get; set; } = obtainedFields;
    }
}
