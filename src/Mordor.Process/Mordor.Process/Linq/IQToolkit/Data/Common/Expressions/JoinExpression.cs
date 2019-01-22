using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    /// <summary>
    /// A custom expression node representing a SQL join clause
    /// </summary>
    public class JoinExpression : DbExpression
    {
        public JoinExpression(JoinType joinType, Expression left, Expression right, Expression condition)
            : base(DbExpressionType.Join, typeof(void))
        {
            Join = joinType;
            Left = left;
            Right = right;
            Condition = condition;
        }
        public JoinType Join { get; }

        public Expression Left { get; }

        public Expression Right { get; }

        public Expression Condition { get; }
    }
}