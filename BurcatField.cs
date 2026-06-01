using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace BurcatProtocol
{
    [BurcatIdentity("00000000-0000-0000-0000-0e1c35c74b6f")]
    public sealed class BurcatField : IBurcatObject
    {
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

        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public string Name { get; }
        public object? Value { get; }

        public BurcatField(string name, object? value) { Name = name; Value = value is NothingChart ? null : value; }

        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        bool IBurcatObject.SetBurcatField(BurcatField field) => false;
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([Name, Value]);
    }
}
