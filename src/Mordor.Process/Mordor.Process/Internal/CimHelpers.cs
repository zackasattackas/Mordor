using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;

namespace Mordor.Process.Internal
{
    internal static class CimHelpers
    {
        public static CimSession LocalHost => CimSession.Create(".");
        public const string DefaultNamespace = "root\\cimv2";

        public static CimSession Connect(string computerName, NetworkCredential credentials)
        {
            throw new NotImplementedException();
        }

        public static IEnumerable<CimInstance> ExecuteWql(CimSession session, string query, string namespaceName = "root\\cimv2")
        {
            return session.QueryInstances(namespaceName, "WQL", query, new CimOperationOptions());
        }

        public static T BindCimInstance<T>(CimInstance instance)
        {
            return instance is null ? default : BindCimInstance(Activator.CreateInstance<T>(), instance);
        }

        public static IEnumerable<T> BindMany<T>(IEnumerable<CimInstance> instances)
        {
            return instances.Select(BindCimInstance<T>);
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