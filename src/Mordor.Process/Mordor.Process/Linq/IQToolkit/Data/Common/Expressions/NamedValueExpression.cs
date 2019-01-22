using System;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class NamedValueExpression : DbExpression
    {
        public NamedValueExpression(string name, QueryType queryType, Expression value)
            : base(DbExpressionType.NamedValue, value.Type)
        {
            //if (queryType == null)
            //throw new ArgumentNullException("queryType");
            Name = name ?? throw new ArgumentNullException(nameof(name));
            QueryType = queryType;
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string Name { get; }

        public QueryType QueryType { get; }

        public Expression Value { get; }
    }
}