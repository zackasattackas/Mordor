using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class ClientJoinExpression : DbExpression
    {
        public ClientJoinExpression(ProjectionExpression projection, IEnumerable<Expression> outerKey, IEnumerable<Expression> innerKey)
            : base(DbExpressionType.ClientJoin, projection.Type)
        {
            OuterKey = outerKey.ToReadOnly();
            InnerKey = innerKey.ToReadOnly();
            Projection = projection;
        }

        public ReadOnlyCollection<Expression> OuterKey { get; }

        public ReadOnlyCollection<Expression> InnerKey { get; }

        public ProjectionExpression Projection { get; }
    }
}