using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class RowNumberExpression : DbExpression
    {
        public RowNumberExpression(IEnumerable<OrderExpression> orderBy)
            : base(DbExpressionType.RowCount, typeof(int))
        {
            OrderBy = orderBy.ToReadOnly();
        }
        public ReadOnlyCollection<OrderExpression> OrderBy { get; }
    }
}