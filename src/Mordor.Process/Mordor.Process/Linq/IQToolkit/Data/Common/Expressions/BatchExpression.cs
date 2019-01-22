using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Expressions
{
    public class BatchExpression : Expression
    {
        public BatchExpression(Expression input, LambdaExpression operation, Expression batchSize, Expression stream)
        {
            Input = input;
            Operation = operation;
            BatchSize = batchSize;
            Stream = stream;
            Type = typeof(IEnumerable<>).MakeGenericType(operation.Body.Type);
        }

        public override ExpressionType NodeType => (ExpressionType)DbExpressionType.Batch;

        public override Type Type { get; }

        public Expression Input { get; }

        public LambdaExpression Operation { get; }

        public Expression BatchSize { get; }

        public Expression Stream { get; }
    }
}