using System;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq.IQToolkit.Data.Common
{
    public class QueryParameter
    {
        public QueryParameter(string name, Type type, QueryType queryType)
        {
            Name = name;
            Type = type;
            QueryType = queryType;
        }

        public string Name { get; }

        public Type Type { get; }

        public QueryType QueryType { get; }
    }
}