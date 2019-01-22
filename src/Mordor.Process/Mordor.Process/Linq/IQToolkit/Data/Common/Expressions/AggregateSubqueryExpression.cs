using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class AggregateSubqueryExpression : DbExpression
    {
        public AggregateSubqueryExpression(TableAlias groupByAlias, Expression aggregateInGroupSelect, ScalarExpression aggregateAsSubquery)
            : base(DbExpressionType.AggregateSubquery, aggregateAsSubquery.Type)
        {
            AggregateInGroupSelect = aggregateInGroupSelect;
            GroupByAlias = groupByAlias;
            AggregateAsSubquery = aggregateAsSubquery;
        }
        public TableAlias GroupByAlias { get; }

        public Expression AggregateInGroupSelect { get; }

        public ScalarExpression AggregateAsSubquery { get; }
    }
}