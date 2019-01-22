using System;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class ScalarExpression : SubqueryExpression
    {
        public ScalarExpression(Type type, SelectExpression select)
            : base(DbExpressionType.Scalar, type, select)
        {
        }
    }
}