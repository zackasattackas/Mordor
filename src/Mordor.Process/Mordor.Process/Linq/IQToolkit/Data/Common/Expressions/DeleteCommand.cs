using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class DeleteCommand : CommandExpression
    {
        public DeleteCommand(TableExpression table, Expression where)
            : base(DbExpressionType.Delete, typeof(int))
        {
            Table = table;
            Where = where;
        }

        public TableExpression Table { get; }

        public Expression Where { get; }
    }
}