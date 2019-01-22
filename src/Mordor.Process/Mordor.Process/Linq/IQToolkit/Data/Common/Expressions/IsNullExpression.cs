using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    /// <summary>
    /// Allows is-null tests against value-types like int and float
    /// </summary>
    public class IsNullExpression : DbExpression
    {
        public IsNullExpression(Expression expression)
            : base(DbExpressionType.IsNull, typeof(bool))
        {
            Expression = expression;
        }
        public Expression Expression { get; }
    }
}