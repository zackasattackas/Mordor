using System;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public abstract class DbExpression : Expression
    {
        private readonly DbExpressionType _expressionType;

        protected DbExpression(DbExpressionType eType, Type type)
        {
            _expressionType = eType;
            Type = type;
        }

        public override ExpressionType NodeType => (ExpressionType)(int)_expressionType;

        public override Type Type { get; }

        public override string ToString()
        {
            return DbExpressionWriter.WriteToString(this);
        }
    }
}