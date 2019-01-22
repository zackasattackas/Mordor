using System.Collections.Generic;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    /// <summary>
    /// A custom expression representing the construction of one or more result objects from a 
    /// SQL select expression
    /// </summary>
    public class ProjectionExpression : DbExpression
    {
        public ProjectionExpression(SelectExpression source, Expression projector)
            : this(source, projector, null)
        {
        }
        public ProjectionExpression(SelectExpression source, Expression projector, LambdaExpression aggregator)
            : base(DbExpressionType.Projection, aggregator != null ? aggregator.Body.Type : typeof(IEnumerable<>).MakeGenericType(projector.Type))
        {
            Select = source;
            Projector = projector;
            Aggregator = aggregator;
        }
        public SelectExpression Select { get; }

        public Expression Projector { get; }

        public LambdaExpression Aggregator { get; }

        public bool IsSingleton => Aggregator != null && Aggregator.Body.Type == Projector.Type;

        public override string ToString()
        {
            return DbExpressionWriter.WriteToString(this);
        }
        public string QueryText => SqlFormatter.Format(Select, true);
    }
}