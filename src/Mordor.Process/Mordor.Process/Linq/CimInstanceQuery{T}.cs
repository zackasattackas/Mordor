using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Mordor.Process.Linq
{
    public class CimInstanceQuery<T> : IOrderedQueryable<T>
    {
        private readonly CimQueryProvider _provider;

        public Type ElementType { get; }
        public Expression Expression { get; }
        public IQueryProvider Provider => _provider;

        public CimInstanceQuery(CimQueryProvider provider, Expression expression)
        {
            _provider = provider;

            ElementType = typeof(T);
            Expression = expression;            
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>) Provider.Execute(Expression)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return _provider.GetQueryText(Expression);
        }
    }
}
