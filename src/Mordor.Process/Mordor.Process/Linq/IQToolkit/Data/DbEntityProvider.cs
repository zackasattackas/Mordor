// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Mordor.Process.Linq.IQToolkit.Data.Common;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;
using Mordor.Process.Linq.IQToolkit.Data.Common.Mapping;

namespace Mordor.Process.Linq.IQToolkit.Data
{
    public class DbEntityProvider : EntityProvider
    {
        private readonly DbConnection _connection;
        private DbTransaction _transaction;

        private int _nConnectedActions;

        public DbEntityProvider(DbConnection connection, QueryLanguage language, QueryMapping mapping, QueryPolicy policy)
            : base(language, mapping, policy)
        {
            _connection = connection ?? throw new InvalidOperationException("Connection not specified");
        }

        public virtual DbConnection Connection => _connection;

        public virtual DbTransaction Transaction
        {
            get => _transaction;
            set
            {
                if (value != null && value.Connection != _connection)
                    throw new InvalidOperationException("Transaction does not match connection.");
                _transaction = value;
            }
        }

        public IsolationLevel Isolation { get; set; } = IsolationLevel.ReadCommitted;

        public virtual DbEntityProvider New(DbConnection connection, QueryMapping mapping, QueryPolicy policy)
        {
            return (DbEntityProvider)Activator.CreateInstance(GetType(), connection, mapping, policy);
        }

        public virtual DbEntityProvider New(DbConnection connection)
        {
            var n = New(connection, Mapping, Policy);
            n.Log = Log;
            return n;
        }

        public virtual DbEntityProvider New(QueryMapping mapping)
        {
            var n = New(Connection, mapping, Policy);
            n.Log = Log;
            return n;
        }

        public virtual DbEntityProvider New(QueryPolicy policy)
        {
            var n = New(Connection, Mapping, policy);
            n.Log = Log;
            return n;
        }

        public static DbEntityProvider FromApplicationSettings()
        {
            var provider = ConfigurationManager.AppSettings["Provider"];
            var connection = ConfigurationManager.AppSettings["Connection"];
            var mapping = ConfigurationManager.AppSettings["Mapping"];
            return From(provider, connection, mapping);
        }

        public static DbEntityProvider From(string connectionString, string mappingId)
        {
            return From(connectionString, mappingId, QueryPolicy.Default);
        }

        public static DbEntityProvider From(string connectionString, string mappingId, QueryPolicy policy)
        {
            return From(null, connectionString, mappingId, policy);
        }

        public static DbEntityProvider From(string connectionString, QueryMapping mapping, QueryPolicy policy)
        {
            return From((string)null, connectionString, mapping, policy);
        }

        public static DbEntityProvider From(string provider, string connectionString, string mappingId)
        {
            return From(provider, connectionString, mappingId, QueryPolicy.Default);
        }

        public static DbEntityProvider From(string provider, string connectionString, string mappingId, QueryPolicy policy)
        {
            return From(provider, connectionString, GetMapping(mappingId), policy);
        }

        public static DbEntityProvider From(string provider, string connectionString, QueryMapping mapping, QueryPolicy policy)
        {
            if (provider == null)
            {
                var clower = connectionString.ToLower();
                // try sniffing connection to figure out provider
                if (clower.Contains(".mdb") || clower.Contains(".accdb"))
                {
                    provider = "IQToolkit.Data.Access";
                }
                else if (clower.Contains(".sdf"))
                {
                    provider = "IQToolkit.Data.SqlServerCe";
                }
                else if (clower.Contains(".sl3") || clower.Contains(".db3"))
                {
                    provider = "IQToolkit.Data.SQLite";
                }
                else if (clower.Contains(".mdf"))
                {
                    provider = "IQToolkit.Data.SqlClient";
                }
                else
                {
                    throw new InvalidOperationException("Query provider not specified and cannot be inferred.");
                }
            }

            var providerType = GetProviderType(provider);
            if (providerType == null)
                throw new InvalidOperationException(string.Format("Unable to find query provider '{0}'", provider));

            return From(providerType, connectionString, mapping, policy);
        }

        public static DbEntityProvider From(Type providerType, string connectionString, QueryMapping mapping, QueryPolicy policy)
        {
            var adoConnectionType = GetAdoConnectionType(providerType);
            if (adoConnectionType == null)
                throw new InvalidOperationException(string.Format("Unable to deduce ADO provider for '{0}'", providerType.Name));
            var connection = (DbConnection)Activator.CreateInstance(adoConnectionType);

            // is the connection string just a filename?
            if (!connectionString.Contains('='))
            {
                var gcs = providerType.GetMethod("GetConnectionString", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string) }, null);
                if (gcs != null)
                {
                    var getConnectionString = (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>), gcs);
                    connectionString = getConnectionString(connectionString);
                }
            }

            connection.ConnectionString = connectionString;

            return (DbEntityProvider)Activator.CreateInstance(providerType, connection, mapping, policy);
        }

        private static Type GetAdoConnectionType(Type providerType)
        {
            // sniff constructors 
            foreach (var con in providerType.GetConstructors())
            {
                foreach (var arg in con.GetParameters())
                {
                    if (arg.ParameterType.IsSubclassOf(typeof(DbConnection)))
                        return arg.ParameterType;
                }
            }
            return null;
        }

        protected bool ActionOpenedConnection { get; private set; }

        protected void StartUsingConnection()
        {
            if (_connection.State == ConnectionState.Closed)
            {
                _connection.Open();
                ActionOpenedConnection = true;
            }
            _nConnectedActions++;
        }

        protected void StopUsingConnection()
        {
            Debug.Assert(_nConnectedActions > 0);
            _nConnectedActions--;
            if (_nConnectedActions == 0 && ActionOpenedConnection)
            {
                _connection.Close();
                ActionOpenedConnection = false;
            }
        }

        public override void DoConnected(Action action)
        {
            StartUsingConnection();
            try
            {
                action();
            }
            finally
            {
                StopUsingConnection();
            }
        }

        public override void DoTransacted(Action action)
        {
            StartUsingConnection();
            try
            {
                if (Transaction == null)
                {
                    var trans = Connection.BeginTransaction(Isolation);
                    try
                    {
                        Transaction = trans;
                        action();
                        trans.Commit();
                    }
                    finally
                    {
                        Transaction = null;
                        trans.Dispose();
                    }
                }
                else
                {
                    action();
                }
            }
            finally
            {
                StopUsingConnection();
            }
        }

        public override int ExecuteCommand(string commandText)
        {
            if (Log != null)
            {
                Log.WriteLine(commandText);
            }
            StartUsingConnection();
            try
            {
                var cmd = Connection.CreateCommand();
                cmd.CommandText = commandText;
                return cmd.ExecuteNonQuery();
            }
            finally
            {
                StopUsingConnection();
            }
        }

        protected override QueryExecutor CreateExecutor()
        {
            return new Executor(this);
        }

        public class Executor : QueryExecutor
        {
            private int _rowsAffected;

            public Executor(DbEntityProvider provider)
            {
                Provider = provider;
            }

            public DbEntityProvider Provider { get; }

            public override int RowsAffected => _rowsAffected;

            protected virtual bool BufferResultRows => false;

            protected bool ActionOpenedConnection => Provider.ActionOpenedConnection;

            protected void StartUsingConnection()
            {
                Provider.StartUsingConnection();
            }

            protected void StopUsingConnection()
            {
                Provider.StopUsingConnection();
            }

            public override object Convert(object value, Type type)
            {
                if (value == null)
                {
                    return TypeHelper.GetDefault(type);
                }
                type = TypeHelper.GetNonNullableType(type);
                var vtype = value.GetType();
                if (type != vtype)
                {
                    if (type.IsEnum)
                    {
                        if (vtype == typeof(string))
                        {
                            return Enum.Parse(type, (string)value);
                        }

                        var utype = Enum.GetUnderlyingType(type);
                        if (utype != vtype)
                        {
                            value = System.Convert.ChangeType(value, utype);
                        }
                        return Enum.ToObject(type, value);
                    }
                    return System.Convert.ChangeType(value, type);
                }
                return value;
            }

            public override IEnumerable<T> Execute<T>(QueryCommand command, Func<FieldReader, T> fnProjector, MappingEntity entity, object[] paramValues)
            {
                LogCommand(command, paramValues);
                StartUsingConnection();
                try
                {
                    var cmd = GetCommand(command, paramValues);
                    var reader = ExecuteReader(cmd);
                    var result = Project(reader, fnProjector, entity, true);
                    if (Provider.ActionOpenedConnection)
                    {
                        result = result.ToList();
                    }
                    else
                    {
                        result = new EnumerateOnce<T>(result);
                    }
                    return result;
                }
                finally
                {
                    StopUsingConnection();
                }
            }

            protected virtual DbDataReader ExecuteReader(DbCommand command)
            {
                var reader = command.ExecuteReader();
                if (BufferResultRows)
                {
                    // use data table to buffer results
                    var ds = new DataSet();
                    ds.EnforceConstraints = false;
                    var table = new DataTable();
                    ds.Tables.Add(table);
                    ds.EnforceConstraints = false;
                    table.Load(reader);
                    reader = table.CreateDataReader();
                }
                return reader;
            }

            protected virtual IEnumerable<T> Project<T>(DbDataReader reader, Func<FieldReader, T> fnProjector, MappingEntity entity, bool closeReader)
            {
                var freader = new DbFieldReader(this, reader);
                try
                {
                    while (reader.Read())
                    {
                        yield return fnProjector(freader);
                    }
                }
                finally
                {
                    if (closeReader)
                    {
                        reader.Close();
                    }
                }
            }

            public override int ExecuteCommand(QueryCommand query, object[] paramValues)
            {
                LogCommand(query, paramValues);
                StartUsingConnection();
                try
                {
                    var cmd = GetCommand(query, paramValues);
                    _rowsAffected = cmd.ExecuteNonQuery();
                    return _rowsAffected;
                }
                finally
                {
                    StopUsingConnection();
                }
            }

            public override IEnumerable<int> ExecuteBatch(QueryCommand query, IEnumerable<object[]> paramSets, int batchSize, bool stream)
            {
                StartUsingConnection();
                try
                {
                    var result = ExecuteBatch(query, paramSets);
                    if (!stream || ActionOpenedConnection)
                    {
                        return result.ToList();
                    }
                    else
                    {
                        return new EnumerateOnce<int>(result);
                    }
                }
                finally
                {
                    StopUsingConnection();
                }
            }

            private IEnumerable<int> ExecuteBatch(QueryCommand query, IEnumerable<object[]> paramSets)
            {
                LogCommand(query, null);
                var cmd = GetCommand(query, null);
                foreach (var paramValues in paramSets)
                {
                    LogParameters(query, paramValues);
                    LogMessage("");
                    SetParameterValues(query, cmd, paramValues);
                    _rowsAffected = cmd.ExecuteNonQuery();
                    yield return _rowsAffected;
                }
            }

            public override IEnumerable<T> ExecuteBatch<T>(QueryCommand query, IEnumerable<object[]> paramSets, Func<FieldReader, T> fnProjector, MappingEntity entity, int batchSize, bool stream)
            {
                StartUsingConnection();
                try
                {
                    var result = ExecuteBatch(query, paramSets, fnProjector);
                    if (!stream || ActionOpenedConnection)
                    {
                        return result.ToList();
                    }
                    else
                    {
                        return new EnumerateOnce<T>(result);
                    }
                }
                finally
                {
                    StopUsingConnection();
                }
            }

            private IEnumerable<T> ExecuteBatch<T>(QueryCommand query, IEnumerable<object[]> paramSets, Func<FieldReader, T> fnProjector)
            {
                LogCommand(query, null);
                var cmd = GetCommand(query, null);
                cmd.Prepare();
                foreach (var paramValues in paramSets)
                {
                    LogParameters(query, paramValues);
                    LogMessage("");
                    SetParameterValues(query, cmd, paramValues);
                    var reader = ExecuteReader(cmd);
                    var freader = new DbFieldReader(this, reader);
                    try
                    {
                        if (reader.HasRows)
                        {
                            reader.Read();
                            yield return fnProjector(freader);
                        }
                        else
                        {
                            yield return default;
                        }
                    }
                    finally
                    {
                        reader.Close();
                    }
                }
            }

            public override IEnumerable<T> ExecuteDeferred<T>(QueryCommand query, Func<FieldReader, T> fnProjector, MappingEntity entity, object[] paramValues)
            {
                LogCommand(query, paramValues);
                StartUsingConnection();
                try
                {
                    var cmd = GetCommand(query, paramValues);
                    var reader = ExecuteReader(cmd);
                    var freader = new DbFieldReader(this, reader);
                    try
                    {
                        while (reader.Read())
                        {
                            yield return fnProjector(freader);
                        }
                    }
                    finally
                    {
                        reader.Close();
                    }
                }
                finally
                {
                    StopUsingConnection();
                }
            }

            /// <summary>
            /// Get an ADO command object initialized with the command-text and parameters
            /// </summary>
            protected virtual DbCommand GetCommand(QueryCommand query, object[] paramValues)
            {
                // create command object (and fill in parameters)
                var cmd = Provider.Connection.CreateCommand();
                cmd.CommandText = query.CommandText;
                if (Provider.Transaction != null)
                    cmd.Transaction = Provider.Transaction;
                SetParameterValues(query, cmd, paramValues);
                return cmd;
            }

            protected virtual void SetParameterValues(QueryCommand query, DbCommand command, object[] paramValues)
            {
                if (query.Parameters.Count > 0 && command.Parameters.Count == 0)
                {
                    for (int i = 0, n = query.Parameters.Count; i < n; i++)
                    {
                        AddParameter(command, query.Parameters[i], paramValues != null ? paramValues[i] : null);
                    }
                }
                else if (paramValues != null)
                {
                    for (int i = 0, n = command.Parameters.Count; i < n; i++)
                    {
                        var p = command.Parameters[i];
                        if (p.Direction == ParameterDirection.Input
                         || p.Direction == ParameterDirection.InputOutput)
                        {
                            p.Value = paramValues[i] ?? DBNull.Value;
                        }
                    }
                }
            }

            protected virtual void AddParameter(DbCommand command, QueryParameter parameter, object value)
            {
                var p = command.CreateParameter();
                p.ParameterName = parameter.Name;
                p.Value = value ?? DBNull.Value;
                command.Parameters.Add(p);
            }

            protected virtual void GetParameterValues(DbCommand command, object[] paramValues)
            {
                if (paramValues != null)
                {
                    for (int i = 0, n = command.Parameters.Count; i < n; i++)
                    {
                        if (command.Parameters[i].Direction != ParameterDirection.Input)
                        {
                            var value = command.Parameters[i].Value;
                            if (value == DBNull.Value)
                                value = null;
                            paramValues[i] = value;
                        }
                    }
                }
            }

            protected virtual void LogMessage(string message)
            {
                if (Provider.Log != null)
                {
                    Provider.Log.WriteLine(message);
                }
            }

            /// <summary>
            /// Write a command and parameters to the log
            /// </summary>
            /// <param name="command"></param>
            /// <param name="paramValues"></param>
            protected virtual void LogCommand(QueryCommand command, object[] paramValues)
            {
                if (Provider.Log != null)
                {
                    Provider.Log.WriteLine(command.CommandText);
                    if (paramValues != null)
                    {
                        LogParameters(command, paramValues);
                    }
                    Provider.Log.WriteLine();
                }
            }

            protected virtual void LogParameters(QueryCommand command, object[] paramValues)
            {
                if (Provider.Log != null && paramValues != null)
                {
                    for (int i = 0, n = command.Parameters.Count; i < n; i++)
                    {
                        var p = command.Parameters[i];
                        var v = paramValues[i];

                        if (v == null || v == DBNull.Value)
                        {
                            Provider.Log.WriteLine("-- {0} = NULL", p.Name);
                        }
                        else
                        {
                            Provider.Log.WriteLine("-- {0} = [{1}]", p.Name, v);
                        }
                    }
                }
            }
        }

        protected class DbFieldReader : FieldReader
        {
            private readonly QueryExecutor _executor;
            private readonly DbDataReader _reader;

            public DbFieldReader(QueryExecutor executor, DbDataReader reader)
            {
                _executor = executor;
                _reader = reader;
                Init();
            }

            protected override int FieldCount => _reader.FieldCount;

            protected override Type GetFieldType(int ordinal)
            {
                return _reader.GetFieldType(ordinal);
            }

            protected override bool IsDbNull(int ordinal)
            {
                return _reader.IsDBNull(ordinal);
            }

            protected override T GetValue<T>(int ordinal)
            {
                return (T)_executor.Convert(_reader.GetValue(ordinal), typeof(T));
            }

            protected override Byte GetByte(int ordinal)
            {
                return _reader.GetByte(ordinal);
            }

            protected override Char GetChar(int ordinal)
            {
                return _reader.GetChar(ordinal);
            }

            protected override DateTime GetDateTime(int ordinal)
            {
                return _reader.GetDateTime(ordinal);
            }

            protected override Decimal GetDecimal(int ordinal)
            {
                return _reader.GetDecimal(ordinal);
            }

            protected override Double GetDouble(int ordinal)
            {
                return _reader.GetDouble(ordinal);
            }

            protected override Single GetSingle(int ordinal)
            {
                return _reader.GetFloat(ordinal);
            }

            protected override Guid GetGuid(int ordinal)
            {
                return _reader.GetGuid(ordinal);
            }

            protected override Int16 GetInt16(int ordinal)
            {
                return _reader.GetInt16(ordinal);
            }

            protected override Int32 GetInt32(int ordinal)
            {
                return _reader.GetInt32(ordinal);
            }

            protected override Int64 GetInt64(int ordinal)
            {
                return _reader.GetInt64(ordinal);
            }

            protected override String GetString(int ordinal)
            {
                return _reader.GetString(ordinal);
            }
        }
    }
}
