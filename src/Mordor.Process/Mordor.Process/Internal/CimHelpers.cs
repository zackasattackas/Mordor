using System.Collections.Generic;
using System.Reflection;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using Mordor.Process.Linq;

namespace Mordor.Process.Internal
{
    internal static class CimHelpers
    {
        public static IEnumerable<CimInstance> ExecuteWql(CimSession session, WqlQuery query, string namespaceName = "root\\cimv2")
        {
            return session.QueryInstances(namespaceName, "WQL", query.ToString(), new CimOperationOptions());
        }

        public static T BindCimInstance<T>(T binder, CimInstance instance)
        {
            foreach (var property in typeof(T).GetProperties())
            {
                if (!property.IsDefined(typeof(CimInstanceProperty), false))
                    continue;

                var cimProperty = (CimInstanceProperty)property.GetCustomAttribute(typeof(CimInstanceProperty));

                property.SetValue(binder, instance.CimInstanceProperties[cimProperty.Name ?? property.Name]?.Value);
            }

            return binder;
        }
    }
}