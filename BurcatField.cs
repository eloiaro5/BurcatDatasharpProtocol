using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents a named field or property value transferred by the Burcat protocol.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-0e1c35c74b6f")]
    public sealed class BurcatField : IBurcatObject
    {
        /// <summary>
        /// Creates a protocol field from an instance member access expression.
        /// </summary>
        /// <typeparam name="T">The instance type.</typeparam>
        /// <param name="instance">The instance whose member value is read.</param>
        /// <param name="expression">A member access expression.</param>
        /// <returns>A field containing the member name and current value.</returns>
        public static BurcatField FromExpression<T>(T instance, Expression<Func<T, object?>> expression)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            else
            {
                MemberExpression member = expression.Body switch
                {
                    MemberExpression m => m,
                    UnaryExpression u when u.Operand is MemberExpression m => m,
                    _ => throw new ArgumentException("Expression must be a member access.")
                };

                return new(member.Member.Name, expression.Compile()(instance));
            }
        }

        /// <summary>
        /// Creates a protocol field from a static or captured member access expression.
        /// </summary>
        /// <typeparam name="T">The expression source type.</typeparam>
        /// <param name="expression">A member access expression.</param>
        /// <returns>A field containing the member name and current value.</returns>
        public static BurcatField FromExpression<T>(Expression<Func<object?>> expression)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            else
            {
                MemberExpression member = expression.Body switch
                {
                    MemberExpression m => m,
                    UnaryExpression u when u.Operand is MemberExpression m => m,
                    _ => throw new ArgumentException("Expression must be a member access.")
                };

                return new(member.Member.Name, expression.Compile()());
            }
        }

        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the protocol-visible field or property name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the field or property value.
        /// </summary>
        public object? Value { get; }

        /// <summary>
        /// Initializes a protocol field.
        /// </summary>
        /// <param name="name">The protocol-visible field or property name.</param>
        /// <param name="value">The field or property value.</param>
        public BurcatField(string name, object? value) { Name = name; Value = value is NothingInstance ? null : value; }

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];

        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([Name, Value]);
    }
}
