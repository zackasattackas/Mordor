using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class OuterJoinedExpression : DbExpression
    {
        public OuterJoinedExpression(Expression test, Expression expression)
            : base(DbExpressionType.OuterJoined, expression.Type)
        {
            Test = test;
            Expression = expression;
        }

        public Expression Test { get; }

        public Expression Expression { get; }
    }
}