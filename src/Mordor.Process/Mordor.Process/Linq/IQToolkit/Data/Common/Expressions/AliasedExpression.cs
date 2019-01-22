using System;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public abstract class AliasedExpression : DbExpression
    {
        protected AliasedExpression(DbExpressionType nodeType, Type type, TableAlias alias)
            : base(nodeType, type)
        {
            Alias = alias;
        }
        public TableAlias Alias { get; }
    }
}