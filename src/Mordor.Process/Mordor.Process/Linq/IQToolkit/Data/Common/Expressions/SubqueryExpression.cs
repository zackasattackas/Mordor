using System;
using System.Diagnostics;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public abstract class SubqueryExpression : DbExpression
    {
        protected SubqueryExpression(DbExpressionType eType, Type type, SelectExpression select)
            : base(eType, type)
        {
            Debug.Assert(eType == DbExpressionType.Scalar || eType == DbExpressionType.Exists || eType == DbExpressionType.In);
            Select = select;
        }
        public SelectExpression Select { get; }
    }
}