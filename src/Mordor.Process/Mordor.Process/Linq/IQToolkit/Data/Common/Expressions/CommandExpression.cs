using System;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public abstract class CommandExpression : DbExpression
    {
        protected CommandExpression(DbExpressionType eType, Type type)
            : base(eType, type)
        {
        }
    }
}