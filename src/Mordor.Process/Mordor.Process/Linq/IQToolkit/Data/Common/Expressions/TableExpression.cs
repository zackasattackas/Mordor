using Mordor.Process.Linq.IQToolkit.Data.Common.Mapping;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    /// <summary>
    /// A custom expression node that represents a table reference in a SQL query
    /// </summary>
    public class TableExpression : AliasedExpression
    {
        public TableExpression(TableAlias alias, MappingEntity entity, string name)
            : base(DbExpressionType.Table, typeof(void), alias)
        {
            Entity = entity;
            Name = name;
        }

        public MappingEntity Entity { get; }

        public string Name { get; }

        public override string ToString()
        {
            return "T(" + Name + ")";
        }
    }
}