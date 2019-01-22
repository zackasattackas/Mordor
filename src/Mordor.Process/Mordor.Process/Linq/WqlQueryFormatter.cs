using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq
{
    internal class WqlQueryFormatter : SqlFormatter
    {
        protected WqlQueryFormatter(QueryLanguage language) 
            : base(language)
        {
        }
    }
}
