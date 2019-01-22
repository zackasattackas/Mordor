using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Mapping;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class EntityExpression : DbExpression
    {
        public EntityExpression(MappingEntity entity, Expression expression)
            : base(DbExpressionType.Entity, expression.Type)
        {
            Entity = entity;
            Expression = expression;
        }

        public MappingEntity Entity { get; }

        public Expression Expression { get; }
    }
}