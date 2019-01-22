using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class ColumnAssignment
    {
        public ColumnAssignment(ColumnExpression column, Expression expression)
        {
            Column = column;
            Expression = expression;
        }

        public ColumnExpression Column { get; }

        public Expression Expression { get; }
    }
}