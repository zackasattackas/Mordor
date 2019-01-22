using System;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    /// <summary>
    /// A custom expression node that represents a reference to a column in a SQL query
    /// </summary>
    public class ColumnExpression : DbExpression, IEquatable<ColumnExpression>
    {
        public ColumnExpression(Type type, QueryType queryType, TableAlias alias, string name)
            : base(DbExpressionType.Column, type)
        {
            Alias = alias;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            QueryType = queryType ?? throw new ArgumentNullException(nameof(queryType));
        }

        public TableAlias Alias { get; }

        public string Name { get; }

        public QueryType QueryType { get; }

        public override string ToString()
        {
            return Alias + ".C(" + Name + ")";
        }

        public override int GetHashCode()
        {
            return Alias.GetHashCode() + Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ColumnExpression);
        }

        public bool Equals(ColumnExpression other)
        {
            return other != null
                   && this == other
                   || (Alias == other.Alias && Name == other.Name);
        }
    }
}