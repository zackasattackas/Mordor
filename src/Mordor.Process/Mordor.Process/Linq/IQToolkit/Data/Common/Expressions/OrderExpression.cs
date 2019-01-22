using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    /// <summary>
    /// A pairing of an expression and an order type for use in a SQL Order By clause
    /// </summary>
    public class OrderExpression
    {
        public OrderExpression(OrderType orderType, Expression expression)
        {
            OrderType = orderType;
            Expression = expression;
        }
        public OrderType OrderType { get; }

        public Expression Expression { get; }
    }
}