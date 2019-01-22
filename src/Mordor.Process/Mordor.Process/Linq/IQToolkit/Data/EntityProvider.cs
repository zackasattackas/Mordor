// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Mordor.Process.Linq.IQToolkit.Data.Common;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;
using Mordor.Process.Linq.IQToolkit.Data.Common.Mapping;
using Mordor.Process.Linq.IQToolkit.Data.Mapping;

namespace Mordor.Process.Linq.IQToolkit.Data
{
    /// <summary>
    /// A LINQ IQueryable query provider that executes database queries over a DbConnection
    /// </summary>
    public abstract class EntityProvider : QueryProvider, IEntityProvider, ICreateExecutor
    {
        private QueryPolicy _policy;
        private readonly Dictionary<MappingEntity, IEntityTable> _tables;

        public EntityProvider(QueryLanguage language, QueryMapping mapping, QueryPolicy policy)
        {
            Language = language ?? throw new InvalidOperationException("Language not specified");
            Mapping = mapping ?? throw new InvalidOperationException("Mapping not specified");
            _policy = policy ?? throw new InvalidOperationException("Policy not specified");
            _tables = new Dictionary<MappingEntity, IEntityTable>();
        }

        public QueryMapping Mapping { get; }

        public QueryLanguage Language { get; }

        public QueryPolicy Policy
        {
            get => _policy;

            set
            {
                if (value == null)
                {
                    _policy = QueryPolicy.Default;
                }
                else
                {
                    _policy = value;
                }
            }
        }

        public TextWriter Log { get; set; }

        public QueryCache Cache { get; set; }

        public IEntityTable GetTable(MappingEntity entity)
        {
            IEntityTable table;
            if (!_tables.TryGetValue(entity, out table))
            {
                table = CreateTable(entity);
                _tables.Add(entity, table);
            }
            return table;
        }

        protected virtual IEntityTable CreateTable(MappingEntity entity)
        {
            return (IEntityTable) Activator.CreateInstance(
                typeof(EntityTable<>).MakeGenericType(entity.ElementType), this, entity);
        }

        public virtual IEntityTable<T> GetTable<T>()
        {
            return GetTable<T>(null);
        }

        public virtual IEntityTable<T> GetTable<T>(string tableId)
        {
            return (IEntityTable<T>)GetTable(typeof(T), tableId);
        }

        public virtual IEntityTable GetTable(Type type)
        {
            return GetTable(type, null);
        }

        public virtual IEntityTable GetTable(Type type, string tableId)
        {
            return GetTable(Mapping.GetEntity(type, tableId));
        }

        public bool CanBeEvaluatedLocally(Expression expression)
        {
            return Mapping.CanBeEvaluatedLocally(expression);
        }

        public virtual bool CanBeParameter(Expression expression)
        {
            var type = TypeHelper.GetNonNullableType(expression.Type);
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Object:
                    if (expression.Type == typeof(Byte[]) ||
                        expression.Type == typeof(Char[]))
                        return true;
                    return false;
                default:
                    return true;
            }
        }

        protected abstract QueryExecutor CreateExecutor();

        QueryExecutor ICreateExecutor.CreateExecutor()
        {
            return CreateExecutor();
        }

        public class EntityTable<T> : Query<T>, IEntityTable<T>, IHaveMappingEntity
        {
            private readonly EntityProvider _provider;

            public EntityTable(EntityProvider provider, MappingEntity entity)
                : base(provider, typeof(IEntityTable<T>))
            {
                _provider = provider;
                Entity = entity;
            }

            public MappingEntity Entity { get; }

            new public IEntityProvider Provider => _provider;

            public string TableId => Entity.TableId;

            public Type EntityType => Entity.EntityType;

            public T GetById(object id)
            {
                var dbProvider = Provider;
                if (dbProvider != null)
                {
                    var keys = id as IEnumerable<object>;
                    if (keys == null)
                        keys = new[] { id };
                    var query = ((EntityProvider)dbProvider).Mapping.GetPrimaryKeyQuery(Entity, Expression, keys.Select(v => Expression.Constant(v)).ToArray());
                    return Provider.Execute<T>(query);
                }
                return default;
            }

            object IEntityTable.GetById(object id)
            {
                return GetById(id);
            }

            public int Insert(T instance)
            {
                return Updatable.Insert(this, instance);
            }

            int IEntityTable.Insert(object instance)
            {
                return Insert((T)instance);
            }

            public int Delete(T instance)
            {
                return Updatable.Delete(this, instance);
            }

            int IEntityTable.Delete(object instance)
            {
                return Delete((T)instance);
            }

            public int Update(T instance)
            {
                return Updatable.Update(this, instance);
            }

            int IEntityTable.Update(object instance)
            {
                return Update((T)instance);
            }

            public int InsertOrUpdate(T instance)
            {
                return Updatable.InsertOrUpdate(this, instance);
            }

            int IEntityTable.InsertOrUpdate(object instance)
            {
                return InsertOrUpdate((T)instance);
            }
        }

        public override string GetQueryText(Expression expression)
        {
            var plan = GetExecutionPlan(expression);
            var commands = CommandGatherer.Gather(plan).Select(c => c.CommandText).ToArray();
            return string.Join("\n\n", commands);
        }

        private class CommandGatherer : DbExpressionVisitor
        {
            private readonly List<QueryCommand> _commands = new List<QueryCommand>();

            public static ReadOnlyCollection<QueryCommand> Gather(Expression expression)
            {
                var gatherer = new CommandGatherer();
                gatherer.Visit(expression);
                return gatherer._commands.AsReadOnly();
            }

            protected override Expression VisitConstant(ConstantExpression c)
            {
                var qc = c.Value as QueryCommand;
                if (qc != null)
                {
                    _commands.Add(qc);
                }
                return c;
            }
        }

        public string GetQueryPlan(Expression expression)
        {
            var plan = GetExecutionPlan(expression);
            return DbExpressionWriter.WriteToString(Language, plan);
        }

        protected virtual QueryTranslator CreateTranslator()
        {
            return new QueryTranslator(Language, Mapping, _policy);
        }

        public abstract void DoTransacted(Action action);
        public abstract void DoConnected(Action action);
        public abstract int ExecuteCommand(string commandText);

        /// <summary>
        /// Execute the query expression (does translation, etc.)
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public override object Execute(Expression expression)
        {
            var lambda = expression as LambdaExpression;

            if (lambda == null && Cache != null && expression.NodeType != ExpressionType.Constant)
            {
                return Cache.Execute(expression);
            }

            var plan = GetExecutionPlan(expression);

            if (lambda != null)
            {
                // compile & return the execution plan so it can be used multiple times
                var fn = Expression.Lambda(lambda.Type, plan, lambda.Parameters);
#if NOREFEMIT
                    return ExpressionEvaluator.CreateDelegate(fn);
#else
                return fn.Compile();
#endif
            }
            else
            {
                // compile the execution plan and invoke it
                var efn = Expression.Lambda<Func<object>>(Expression.Convert(plan, typeof(object)));
#if NOREFEMIT
                    return ExpressionEvaluator.Eval(efn, new object[] { });
#else
                var fn = efn.Compile();
                return fn();
#endif
            }
        }

        /// <summary>
        /// Convert the query expression into an execution plan
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public virtual Expression GetExecutionPlan(Expression expression)
        {
            // strip off lambda for now
            var lambda = expression as LambdaExpression;
            if (lambda != null)
                expression = lambda.Body;

            var translator = CreateTranslator();

            // translate query into client & server parts
            var translation = translator.Translate(expression);

            var parameters = lambda != null ? lambda.Parameters : null;
            var provider = Find(expression, parameters, typeof(EntityProvider));
            if (provider == null)
            {
                var rootQueryable = Find(expression, parameters, typeof(IQueryable));
                provider = Expression.Property(rootQueryable, typeof(IQueryable).GetProperty("Provider"));
            }

            return translator.Police.BuildExecutionPlan(translation, provider);
        }

        private Expression Find(Expression expression, IList<ParameterExpression> parameters, Type type)
        {
            if (parameters != null)
            {
                Expression found = parameters.FirstOrDefault(p => type.IsAssignableFrom(p.Type));
                if (found != null)
                    return found;
            }

            return TypedSubtreeFinder.Find(expression, type);
        }
           
        public static QueryMapping GetMapping(string mappingId)
        {
            if (mappingId != null)
            {
                var type = FindLoadedType(mappingId);
                if (type != null)
                {
                    return new AttributeMapping(type);
                }

                if (File.Exists(mappingId))
                {
                    return XmlMapping.FromXml(File.ReadAllText(mappingId));
                }
            }

            return new ImplicitMapping();
        }

        public static Type GetProviderType(string providerName)
        {
            if (!string.IsNullOrEmpty(providerName))
            {
                var type = FindInstancesIn(typeof(EntityProvider), providerName).FirstOrDefault();
                if (type != null)
                    return type;
            }
            return null;
        }

        private static Type FindLoadedType(string typeName)
        {
            foreach (var assem in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assem.GetType(typeName, false, true);
                if (type != null)
                    return type;
            }
            return null;
        }

        private static IEnumerable<Type> FindInstancesIn(Type type, string assemblyName)
        {
            var assembly = GetAssemblyForNamespace(assemblyName);
            if (assembly != null)
            {
                foreach (var atype in assembly.GetTypes())
                {
                    if (string.Compare(atype.Namespace, assemblyName, true) == 0
                        && type.IsAssignableFrom(atype))
                    {
                        yield return atype;
                    }
                }
            }
        }

        private static Assembly GetAssemblyForNamespace(string nspace)
        {
            foreach (var assem in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assem.FullName.Contains(nspace))
                {
                    return assem;
                }
            }

            return Load(nspace + ".dll");
        }

        private static Assembly Load(string name)
        {
            // try to load it.
            try
            {
                var fullName = Path.GetFullPath(name);
                return Assembly.LoadFrom(fullName);
            }
            catch
            {
            }
            return null;
        }
    }
}
