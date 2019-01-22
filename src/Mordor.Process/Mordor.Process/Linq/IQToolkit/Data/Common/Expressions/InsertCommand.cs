using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class InsertCommand : CommandExpression
    {
        public InsertCommand(TableExpression table, IEnumerable<ColumnAssignment> assignments)
            : base(DbExpressionType.Insert, typeof(int))
        {
            Table = table;
            Assignments = assignments.ToReadOnly();
        }

        public TableExpression Table { get; }

        public ReadOnlyCollection<ColumnAssignment> Assignments { get; }
    }
}