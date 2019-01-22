using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    /// <summary>
    /// A custom expression node used to represent a SQL SELECT expression
    /// </summary>
    public class SelectExpression : AliasedExpression
    {
        public SelectExpression(
            TableAlias alias,
            IEnumerable<ColumnDeclaration> columns,
            Expression from,
            Expression where,
            IEnumerable<OrderExpression> orderBy,
            IEnumerable<Expression> groupBy,
            bool isDistinct,
            Expression skip,
            Expression take,
            bool reverse
        )
            : base(DbExpressionType.Select, typeof(void), alias)
        {
            Columns = columns.ToReadOnly();
            IsDistinct = isDistinct;
            From = from;
            Where = where;
            OrderBy = orderBy.ToReadOnly();
            GroupBy = groupBy.ToReadOnly();
            Take = take;
            Skip = skip;
            IsReverse = reverse;
        }
        public SelectExpression(
            TableAlias alias,
            IEnumerable<ColumnDeclaration> columns,
            Expression from,
            Expression where,
            IEnumerable<OrderExpression> orderBy,
            IEnumerable<Expression> groupBy
        )
            : this(alias, columns, from, where, orderBy, groupBy, false, null, null, false)
        {
        }
        public SelectExpression(
            TableAlias alias, IEnumerable<ColumnDeclaration> columns,
            Expression from, Expression where
        )
            : this(alias, columns, from, where, null, null)
        {
        }
        public ReadOnlyCollection<ColumnDeclaration> Columns { get; }

        public Expression From { get; }

        public Expression Where { get; }

        public ReadOnlyCollection<OrderExpression> OrderBy { get; }

        public ReadOnlyCollection<Expression> GroupBy { get; }

        public bool IsDistinct { get; }

        public Expression Skip { get; }

        public Expression Take { get; }

        public bool IsReverse { get; }

        public string QueryText => SqlFormatter.Format(this, true);
    }
}