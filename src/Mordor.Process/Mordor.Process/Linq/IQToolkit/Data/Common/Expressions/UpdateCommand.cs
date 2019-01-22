using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class UpdateCommand : CommandExpression
    {
        public UpdateCommand(TableExpression table, Expression where, IEnumerable<ColumnAssignment> assignments)
            : base(DbExpressionType.Update, typeof(int))
        {
            Table = table;
            Where = where;
            Assignments = assignments.ToReadOnly();
        }

        public TableExpression Table { get; }

        public Expression Where { get; }

        public ReadOnlyCollection<ColumnAssignment> Assignments { get; }
    }
}