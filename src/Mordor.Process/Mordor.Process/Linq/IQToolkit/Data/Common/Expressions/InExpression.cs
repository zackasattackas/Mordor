using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class InExpression : SubqueryExpression
    {
        public InExpression(Expression expression, SelectExpression select)
            : base(DbExpressionType.In, typeof(bool), select)
        {
            Expression = expression;
        }
        public InExpression(Expression expression, IEnumerable<Expression> values)
            : base(DbExpressionType.In, typeof(bool), null)
        {
            Expression = expression;
            Values = values.ToReadOnly();
        }
        public Expression Expression { get; }

        public ReadOnlyCollection<Expression> Values { get; }
    }
}