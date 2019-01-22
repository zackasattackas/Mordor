using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class FunctionExpression : DbExpression
    {
        public FunctionExpression(Type type, string name, IEnumerable<Expression> arguments)
            : base(DbExpressionType.Function, type)
        {
            Name = name;
            Arguments = arguments.ToReadOnly();
        }

        public string Name { get; }

        public ReadOnlyCollection<Expression> Arguments { get; }
    }
}