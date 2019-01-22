using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class VariableDeclaration
    {
        public VariableDeclaration(string name, QueryType type, Expression expression)
        {
            Name = name;
            QueryType = type;
            Expression = expression;
        }

        public string Name { get; }

        public QueryType QueryType { get; }

        public Expression Expression { get; }
    }
}