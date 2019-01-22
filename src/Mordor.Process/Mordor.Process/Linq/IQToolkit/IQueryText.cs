using System.Linq;
using System.Linq.Expressions;

namespace Mordor.Process.Linq.IQToolkit
{
    /// <summary>
    ///     Optional interface for <see cref="IQueryProvider" /> to implement <see cref="Query{T}.QueryText" /> property.
    /// </summary>
    public interface IQueryText
    {
        string GetQueryText(Expression expression);
    }
}