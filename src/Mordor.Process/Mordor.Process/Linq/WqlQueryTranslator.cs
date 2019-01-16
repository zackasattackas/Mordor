using System;
using System.Linq.Expressions;
using System.Text;

namespace Mordor.Process.Linq
{
    public class WqlQueryTranslator : ExpressionVisitor
    {
        #region Syntax 

        private abstract class WqlToken
        {
            public const string Select = " SELECT ";
            public const string From = " FROM ";
            public const string Where = " WHERE ";

            public const string Equal = " = ";
            public const string NotEqual = " <> ";
            public const string LessThan = " < ";
            public const string GreaterThan = " > ";
            public const string LessThanOrEqual = " <= ";
            public const string GreaterThanOrEqual = " >= ";
            public const string Is = " IS ";
            public const string IsNot = Is + "  NOT ";
            public const string Like = " LIKE ";

            public const string And = " AND ";
            public const string Or = " OR ";
            public const string Not = " NOT ";

            public const string Null = " NULL ";
            public const string True = " TRUE ";
            public const string False = " FALSE ";

            public const string Star = "*";

            public static string Join(params string[] tokens)
            {
                return string.Join(" ", tokens);
            }
        }

        #endregion

        #region Fields

        private readonly StringBuilder _bldr = new StringBuilder(1024);

        #endregion

        #region Protected methods

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            return base.VisitMethodCall(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            _bldr.Append("(");

            Visit(node.Left);

            switch (node.NodeType)
            {
                case ExpressionType.And:
                    _bldr.Append(WqlToken.And);
                    break;
                case ExpressionType.Or:
                    _bldr.Append(WqlToken.Or);
                    break;
                case ExpressionType.Equal:
                    _bldr.Append(WqlToken.Equal);
                    break;
                case ExpressionType.NotEqual:
                    _bldr.Append(WqlToken.NotEqual);
                    break;
                case ExpressionType.LessThan:
                    _bldr.Append(WqlToken.LessThan);
                    break;
                case ExpressionType.LessThanOrEqual:
                    _bldr.Append(WqlToken.LessThanOrEqual);
                    break;
                case ExpressionType.GreaterThan:
                    _bldr.Append(WqlToken.GreaterThan);
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    _bldr.Append(WqlToken.GreaterThanOrEqual);
                    break;
                case ExpressionType.TypeIs:
                    if (!IsNullConstantExpression(node.Right))
                        throw new NotSupportedException("The binary operator " + node.NodeType + " is only supported when comparing to null.");

                    _bldr.Append(WqlToken.Is);
                    break;
                default:
                    throw new NotSupportedException("The binary operator " + node.NodeType + " is not supported.");
            }

            Visit(node.Right);

            return node;
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.Not:
                    _bldr.Append(WqlToken.Not);
                    Visit(node.Operand);
                    break;
                default:
                    throw new NotSupportedException("The unary operator " + node.NodeType + " is not supported.");
            }

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
            { 
                _bldr.Append(node.Member.Name);

                return node;
            }

            throw new NotSupportedException("The member " + node.Member.Name + " is not supported.");
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is null)
                _bldr.Append(WqlToken.Null);
            else
                switch (Type.GetTypeCode(node.Value.GetType()))
                {
                    case TypeCode.Boolean:
                        _bldr.Append((bool) node.Value ? WqlToken.True : WqlToken.False);
                        break;
                    case TypeCode.String:
                        _bldr.Append("'" + node.Value + "'");
                        break;
                    case TypeCode.DateTime:
                        // TODO
                        break;
                    case TypeCode.Object:
                        throw new NotSupportedException("The constant for " + node.Value + " is not supported.");
                    default:
                        _bldr.Append(node.Value);
                        break;
                }

            return node;
        }

        #endregion

        #region Private methods

        private static bool IsNullConstantExpression(Expression expression)
        {
            if (!(expression is ConstantExpression node))
                return false;

            return node.Value is null;
        }

        #endregion
    }
}