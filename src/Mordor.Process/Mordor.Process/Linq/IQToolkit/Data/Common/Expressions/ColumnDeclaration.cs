using System;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    /// <summary>
    /// A declaration of a column in a SQL SELECT expression
    /// </summary>
    public class ColumnDeclaration
    {
        public ColumnDeclaration(string name, Expression expression, QueryType queryType)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            QueryType = queryType ?? throw new ArgumentNullException(nameof(queryType));
        }

        public string Name { get; }

        public Expression Expression { get; }

        public QueryType QueryType { get; }
    }
}