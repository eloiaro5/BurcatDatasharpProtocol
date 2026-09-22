using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Cache
{
    public class BeforeGettingFieldsEventArgs(IBurcatObject requestedObject)
    {
        public IBurcatObject RequestedObject { get; } = requestedObject;
    }

    public class AfterGettingFieldsEventArgs(IBurcatObject requestedObject, BurcatField[] obtainedFields)
    {
        public IBurcatObject RequestedObject { get; } = requestedObject;
        public BurcatField[] ObtainedFields { get; set; } = obtainedFields;
    }
}
