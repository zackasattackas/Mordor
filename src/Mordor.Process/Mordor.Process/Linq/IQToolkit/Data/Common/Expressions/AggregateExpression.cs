using System;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class AggregateExpression : DbExpression
    {
        public AggregateExpression(Type type, string aggregateName, Expression argument, bool isDistinct)
            : base(DbExpressionType.Aggregate, type)
        {
            AggregateName = aggregateName;
            Argument = argument;
            IsDistinct = isDistinct;
        }
        public string AggregateName { get; }

        public Expression Argument { get; }

        public bool IsDistinct { get; }
    }
}