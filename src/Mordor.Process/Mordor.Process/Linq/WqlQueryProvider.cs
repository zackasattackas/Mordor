using System;
using System.Linq.Expressions;
using Microsoft.Management.Infrastructure;
using Mordor.Process.Internal;
using Mordor.Process.Linq.IQToolkit;

namespace Mordor.Process.Linq
{
    public class WqlQueryProvider : QueryProvider
    {
        private readonly object _lock = new object();
        private CimSession _session;
        private string _namespace = CimHelpers.DefaultNamespace;

        public CimSession Session
        {
            get => _session ?? (_session = CimHelpers.LocalHost);
            set
            {
                lock (_lock)
                {
                    _session?.Dispose();
                    _session = value;
                } 
            }
        }

        public string Namespace
        {
            get => _namespace;
            set
            {
                lock (_lock) _namespace = value;
            }
        }

        public WqlQueryProvider(CimSession session, string namespaceName)
        {
            Session = session;
            Namespace = namespaceName;
        }

        public override string GetQueryText(Expression expression)
        {
            throw new NotImplementedException();
        }

        public override object Execute(Expression expression)
        {
            lock (_lock)
                return CimHelpers.ExecuteWql(CimHelpers.LocalHost, GetQueryText(expression));
        }
    }
}