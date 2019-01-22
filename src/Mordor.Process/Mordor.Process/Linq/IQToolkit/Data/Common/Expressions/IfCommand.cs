using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class IfCommand : CommandExpression
    {
        public IfCommand(Expression check, Expression ifTrue, Expression ifFalse)
            : base(DbExpressionType.If, ifTrue.Type)
        {
            Check = check;
            IfTrue = ifTrue;
            IfFalse = ifFalse;
        }

        public Expression Check { get; }

        public Expression IfTrue { get; }

        public Expression IfFalse { get; }
    }
}