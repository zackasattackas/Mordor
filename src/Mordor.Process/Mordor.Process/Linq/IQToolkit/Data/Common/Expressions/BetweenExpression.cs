using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class BetweenExpression : DbExpression
    {
        public BetweenExpression(Expression expression, Expression lower, Expression upper)
            : base(DbExpressionType.Between, expression.Type)
        {
            Expression = expression;
            Lower = lower;
            Upper = upper;
        }
        public Expression Expression { get; }

        public Expression Lower { get; }

        public Expression Upper { get; }
    }
}