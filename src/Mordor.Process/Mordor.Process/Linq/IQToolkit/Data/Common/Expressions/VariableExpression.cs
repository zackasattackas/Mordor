using System;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class VariableExpression : Expression
    {
        public VariableExpression(string name, Type type, QueryType queryType)
        {
            Name = name;
            QueryType = queryType;
            Type = type;
        }

        public override ExpressionType NodeType => (ExpressionType)DbExpressionType.Variable;

        public override Type Type { get; }

        public string Name { get; }

        public QueryType QueryType { get; }
    }
}