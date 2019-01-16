using System;
using System.Linq;
using System.Linq.Expressions;

namespace Mordor.Process.Linq
{
    public class CimQueryProvider : QueryProvider
    {
        public override IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new CimInstanceQuery<TElement>(this, expression);
        }

        public override object Execute(Expression expression)
        {
            throw new NotImplementedException();
        }

        public override string GetQueryText(Expression expression)
        {
            throw new NotImplementedException();
        }
    }
}