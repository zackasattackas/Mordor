using System;
using System.Linq.Expressions;
using System.Reflection;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;

namespace Mordor.Process.Linq
{
    internal class WqlLanguage : QueryLanguage
    {
        public override QueryTypeSystem TypeSystem { get; }
        public override Expression GetGeneratedIdExpression(MemberInfo member)
        {
            throw new NotImplementedException();
        }
    }
}