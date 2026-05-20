using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace BurcatProtocol
{
    [BurcatIdentity("00000000-0000-0000-0000-cd72396d47d9")]
    public sealed class ActionResult : IBurcatObject
    {
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public static ActionResult Unsuccessful { get; } = new();
        public static ActionResult Thrown(BurcatException exception) => new(exception);

        public bool SuccessfulExecution { get; private set; }
        public BurcatException? Exception { get; private set; }
        public IBurcatObject? Value { get; private set; }

        private ActionResult() { SuccessfulExecution = false; Value = null; }
        private ActionResult(BurcatException exception) : this() { Exception = exception; }
        public ActionResult(IBurcatObject? result) { SuccessfulExecution = true; Value = result is NothingChart ? null : result; }

        BurcatField[] IBurcatObject.GetBurcatFields() => [new(nameof(SuccessfulExecution), BurcatTranslator.Translate(SuccessfulExecution)), BurcatField.FromExpression(this, a => a.Exception)];
        bool IBurcatObject.SetBurcatField(BurcatField field)
        {
            if (field.Name == nameof(SuccessfulExecution))
            {
                SuccessfulExecution = BurcatTranslator.Translate<bool>((BurcatTranslation)field.Value!);
                if (!SuccessfulExecution) Value = null;

                return true;
            }
            else if (!SuccessfulExecution && field.Name == nameof(Exception)) { Exception = (BurcatException?)field.Value; return true; }
            else return false;
        }
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [Value];
    }
}
