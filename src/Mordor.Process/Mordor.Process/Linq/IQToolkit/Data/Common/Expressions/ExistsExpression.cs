namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class ExistsExpression : SubqueryExpression
    {
        public ExistsExpression(SelectExpression select)
            : base(DbExpressionType.Exists, typeof(bool), select)
        {
        }
    }
}