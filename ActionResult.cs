using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents the result of executing a Burcat action.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-cd72396d47d9")]
    public sealed class ActionResult : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets a reusable unsuccessful action result with no exception payload.
        /// </summary>
        public static ActionResult Unsuccessful { get; } = new();

        /// <summary>
        /// Creates an unsuccessful action result from a protocol exception.
        /// </summary>
        /// <param name="exception">The exception produced by the action.</param>
        /// <returns>An unsuccessful action result.</returns>
        public static ActionResult Thrown(BurcatException exception) => new(exception);

        /// <summary>
        /// Gets whether the action executed successfully.
        /// </summary>
        public bool SuccessfulExecution { get; private set; }

        /// <summary>
        /// Gets the exception produced by an unsuccessful action.
        /// </summary>
        public BurcatException? Exception { get; private set; }

        /// <summary>
        /// Gets the protocol value returned by a successful action.
        /// </summary>
        public IBurcatObject? Value { get; private set; }

        private ActionResult() { SuccessfulExecution = false; Value = null; }
        private ActionResult(BurcatException exception) : this() { Exception = exception; }
        /// <summary>
        /// Initializes a successful action result.
        /// </summary>
        /// <param name="result">The value returned by the action.</param>
        public ActionResult(IBurcatObject? result) { SuccessfulExecution = true; Value = result is NothingChart ? null : result; }

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [new(nameof(SuccessfulExecution), BurcatTranslator.Translate(SuccessfulExecution)), BurcatField.FromExpression(this, a => a.Exception)];

        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields)
        {
            foreach (BurcatField field in fields)
            {
                if (field.Name == nameof(SuccessfulExecution))
                {
                    SuccessfulExecution = BurcatTranslator.Translate<bool>((BurcatTranslation)field.Value!);
                    if (!SuccessfulExecution) Value = null;
                }
                else if (!SuccessfulExecution && field.Name == nameof(Exception)) Exception = (BurcatException?)field.Value;
            }
        }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [Value];
    }
}
