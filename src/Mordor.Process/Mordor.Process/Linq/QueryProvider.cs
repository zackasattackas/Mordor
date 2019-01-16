using System;
using System.Linq;
using System.Linq.Expressions;

namespace Mordor.Process.Linq
{
    public abstract class QueryProvider : IQueryProvider
    {
        public IQueryable CreateQuery(Expression expression)
        {
            throw new NotImplementedException();
        }

        public abstract IQueryable<TElement> CreateQuery<TElement>(Expression expression);

        public abstract object Execute(Expression expression);

        public TResult Execute<TResult>(Expression expression)
        {
            return (TResult) Execute(expression);
        }

        public abstract string GetQueryText(Expression expression);
    }
}