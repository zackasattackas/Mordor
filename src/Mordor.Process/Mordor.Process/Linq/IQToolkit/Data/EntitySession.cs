// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common;
using Mordor.Process.Linq.IQToolkit.Data.Common.Mapping;

namespace Mordor.Process.Linq.IQToolkit.Data
{
    public class EntitySession : IEntitySession
    {
        private readonly EntityProvider _provider;
        private readonly SessionProvider _sessionProvider;
        private readonly Dictionary<MappingEntity, ISessionTable> _tables;

        public EntitySession(EntityProvider provider)
        {
            _provider = provider;
            _sessionProvider = new SessionProvider(this, provider);
            _tables = new Dictionary<MappingEntity, ISessionTable>();
        }

        public IEntityProvider Provider => _sessionProvider;

        IEntityProvider IEntitySession.Provider => Provider;

        protected IEnumerable<ISessionTable> GetTables()
        {
            return _tables.Values;
        }

        public ISessionTable GetTable(Type elementType, string tableId)
        {
            return GetTable(_sessionProvider.Provider.Mapping.GetEntity(elementType, tableId));
        }

        public ISessionTable<T> GetTable<T>(string tableId)
        {
            return (ISessionTable<T>)GetTable(typeof(T), tableId);
        }

        protected ISessionTable GetTable(MappingEntity entity)
        {
            ISessionTable table;
            if (!_tables.TryGetValue(entity, out table))
            {
                table = CreateTable(entity);
                _tables.Add(entity, table);
            }
            return table;
        }

        private object OnEntityMaterialized(MappingEntity entity, object instance)
        {
            var table = (IEntitySessionTable)GetTable(entity);
            return table.OnEntityMaterialized(instance);
        }

        private interface IEntitySessionTable : ISessionTable
        {
            object OnEntityMaterialized(object instance);
            MappingEntity Entity { get; }
        }

        abstract class SessionTable<T> : Query<T>, ISessionTable<T>, ISessionTable, IEntitySessionTable
        {
            private readonly EntitySession _session;

            public SessionTable(EntitySession session, MappingEntity entity)
                : base(session._sessionProvider, typeof(ISessionTable<T>))
            {
                _session = session;
                Entity = entity;
                ProviderTable = _session.Provider.GetTable<T>(entity.TableId);
            }

            public IEntitySession Session => _session;

            public MappingEntity Entity { get; }

            public IEntityTable<T> ProviderTable { get; }

            IEntityTable ISessionTable.ProviderTable => ProviderTable;

            public T GetById(object id)
            {
                return ProviderTable.GetById(id);
            }

            object ISessionTable.GetById(object id)
            {
                return GetById(id);
            }

            public virtual object OnEntityMaterialized(object instance)
            {
                return instance;
            }

            public virtual void SetSubmitAction(T instance, SubmitAction action)
            {
                throw new NotImplementedException();
            }

            void ISessionTable.SetSubmitAction(object instance, SubmitAction action)
            {
                SetSubmitAction((T)instance, action);
            }

            public virtual SubmitAction GetSubmitAction(T instance)
            {
                throw new NotImplementedException();
            }

            SubmitAction ISessionTable.GetSubmitAction(object instance)
            {
                return GetSubmitAction((T)instance);
            }
        }

        private class SessionProvider : QueryProvider, IEntityProvider, ICreateExecutor
        {
            private readonly EntitySession _session;

            public SessionProvider(EntitySession session, EntityProvider provider)
            {
                _session = session;
                Provider = provider;
            }

            public EntityProvider Provider { get; }

            public override object Execute(Expression expression)
            {
                return Provider.Execute(expression);
            }

            public override string GetQueryText(Expression expression)
            {
                return Provider.GetQueryText(expression);
            }

            public IEntityTable<T> GetTable<T>(string tableId)
            {
                return Provider.GetTable<T>(tableId);
            }

            public IEntityTable GetTable(Type type, string tableId)
            {
                return Provider.GetTable(type, tableId);
            }

            public bool CanBeEvaluatedLocally(Expression expression)
            {
                return Provider.Mapping.CanBeEvaluatedLocally(expression);
            }

            public bool CanBeParameter(Expression expression)
            {
                return Provider.CanBeParameter(expression);
            }

            QueryExecutor ICreateExecutor.CreateExecutor()
            {
                return new SessionExecutor(_session, ((ICreateExecutor)Provider).CreateExecutor());
            }
        }

        private class SessionExecutor : QueryExecutor
        {
            private readonly EntitySession _session;
            private readonly QueryExecutor _executor;

            public SessionExecutor(EntitySession session, QueryExecutor executor)
            {
                _session = session;
                _executor = executor;
            }

            public override int RowsAffected => _executor.RowsAffected;

            public override object Convert(object value, Type type)
            {
                return _executor.Convert(value, type);
            }

            public override IEnumerable<T> Execute<T>(QueryCommand command, Func<FieldReader, T> fnProjector, MappingEntity entity, object[] paramValues)
            {
                return _executor.Execute(command, Wrap(fnProjector, entity), entity, paramValues);
            }

            public override IEnumerable<int> ExecuteBatch(QueryCommand query, IEnumerable<object[]> paramSets, int batchSize, bool stream)
            {
                return _executor.ExecuteBatch(query, paramSets, batchSize, stream);
            }

            public override IEnumerable<T> ExecuteBatch<T>(QueryCommand query, IEnumerable<object[]> paramSets, Func<FieldReader, T> fnProjector, MappingEntity entity, int batchSize, bool stream)
            {
                return _executor.ExecuteBatch(query, paramSets, Wrap(fnProjector, entity), entity, batchSize, stream);
            }

            public override IEnumerable<T> ExecuteDeferred<T>(QueryCommand query, Func<FieldReader, T> fnProjector, MappingEntity entity, object[] paramValues)
            {
                return _executor.ExecuteDeferred(query, Wrap(fnProjector, entity), entity, paramValues);
            }

            public override int ExecuteCommand(QueryCommand query, object[] paramValues)
            {
                return _executor.ExecuteCommand(query, paramValues);
            }

            private Func<FieldReader, T> Wrap<T>(Func<FieldReader, T> fnProjector, MappingEntity entity)
            {
                Func<FieldReader, T> fnWrapped = fr => (T)_session.OnEntityMaterialized(entity, fnProjector(fr));
                return fnWrapped;
            }
        }

        public virtual void SubmitChanges()
        {
            _provider.DoTransacted(
                delegate
                {
                    var submitted = new List<TrackedItem>();

                    // do all submit actions
                    foreach (var item in GetOrderedItems())
                    {
                        if (item.Table.SubmitChanges(item))
                        {
                            submitted.Add(item);
                        }
                    }

                    // on completion, accept changes
                    foreach (var item in submitted)
                    {
                        item.Table.AcceptChanges(item);
                    }
                }
            );
        }

        protected virtual ISessionTable CreateTable(MappingEntity entity)
        {
            return (ISessionTable)Activator.CreateInstance(typeof(TrackedTable<>).MakeGenericType(entity.ElementType), this, entity);
        }

        private interface ITrackedTable : IEntitySessionTable
        {
            object GetFromCacheById(object key);
            IEnumerable<TrackedItem> TrackedItems { get; }
            TrackedItem GetTrackedItem(object instance);
            bool SubmitChanges(TrackedItem item);
            void AcceptChanges(TrackedItem item);
        }

        private class TrackedTable<T> : SessionTable<T>, ITrackedTable
        {
            private readonly Dictionary<T, TrackedItem> _tracked;
            private readonly Dictionary<object, T> _identityCache;

            public TrackedTable(EntitySession session, MappingEntity entity)
                : base(session, entity)
            {
                _tracked = new Dictionary<T, TrackedItem>();
                _identityCache = new Dictionary<object, T>();
            }

            IEnumerable<TrackedItem> ITrackedTable.TrackedItems => _tracked.Values;

            TrackedItem ITrackedTable.GetTrackedItem(object instance)
            {
                TrackedItem ti;
                if (_tracked.TryGetValue((T)instance, out ti))
                    return ti;
                return null;
            }

            object ITrackedTable.GetFromCacheById(object key)
            {
                T cached;
                _identityCache.TryGetValue(key, out cached);
                return cached;
            }

            private bool SubmitChanges(TrackedItem item)
            {
                switch (item.State)
                {
                    case SubmitAction.Delete:
                        ProviderTable.Delete(item.Instance);
                        return true;
                    case SubmitAction.Insert:
                        ProviderTable.Insert(item.Instance);
                        return true;
                    case SubmitAction.InsertOrUpdate:
                        ProviderTable.InsertOrUpdate(item.Instance);
                        return true;
                    case SubmitAction.PossibleUpdate:
                        if (item.Original != null &&
                            Mapping.IsModified(item.Entity, item.Instance, item.Original))
                        {
                            ProviderTable.Update(item.Instance);
                            return true;
                        }
                        break;
                    case SubmitAction.Update:
                        ProviderTable.Update(item.Instance);
                        return true;
                }
                return false;
            }

            bool ITrackedTable.SubmitChanges(TrackedItem item)
            {
                return SubmitChanges(item);
            }

            private void AcceptChanges(TrackedItem item)
            {
                switch (item.State)
                {
                    case SubmitAction.Delete:
                        RemoveFromCache((T)item.Instance);
                        AssignAction((T)item.Instance, SubmitAction.None);
                        break;
                    case SubmitAction.Insert:
                        AddToCache((T)item.Instance);
                        AssignAction((T)item.Instance, SubmitAction.PossibleUpdate);
                        break;
                    case SubmitAction.InsertOrUpdate:
                        AddToCache((T)item.Instance);
                        AssignAction((T)item.Instance, SubmitAction.PossibleUpdate);
                        break;
                    case SubmitAction.PossibleUpdate:
                    case SubmitAction.Update:
                        AssignAction((T)item.Instance, SubmitAction.PossibleUpdate);
                        break;
                }
            }

            void ITrackedTable.AcceptChanges(TrackedItem item)
            {
                AcceptChanges(item);
            }

            public override object OnEntityMaterialized(object instance)
            {
                var typedInstance = (T)instance;
                var cached = AddToCache(typedInstance);
                if ((object)cached == (object)typedInstance)
                {
                    AssignAction(typedInstance, SubmitAction.PossibleUpdate);
                }
                return cached;
            }

            public override SubmitAction GetSubmitAction(T instance)
            {
                TrackedItem ti;
                if (_tracked.TryGetValue(instance, out ti))
                {
                    if (ti.State == SubmitAction.PossibleUpdate)
                    {
                        if (Mapping.IsModified(ti.Entity, ti.Instance, ti.Original))
                        {
                            return SubmitAction.Update;
                        }

                        return SubmitAction.None;
                    }
                    return ti.State;
                }
                return SubmitAction.None;
            }

            public override void SetSubmitAction(T instance, SubmitAction action)
            {
                switch (action)
                {
                    case SubmitAction.None:
                    case SubmitAction.PossibleUpdate:
                    case SubmitAction.Update:
                    case SubmitAction.Delete:
                        var cached = AddToCache(instance);
                        if ((object)cached != (object)instance)
                        {
                            throw new InvalidOperationException("An different instance with the same key is already in the cache.");
                        }
                        break;
                }
                AssignAction(instance, action);
            }

            private QueryMapping Mapping => ((EntitySession)Session)._provider.Mapping;

            private T AddToCache(T instance)
            {
                var key = Mapping.GetPrimaryKey(Entity, instance);
                T cached;
                if (!_identityCache.TryGetValue(key, out cached))
                {
                    cached = instance;
                    _identityCache.Add(key, cached);
                }
                return cached;
            }

            private void RemoveFromCache(T instance)
            {
                var key = Mapping.GetPrimaryKey(Entity, instance);
                _identityCache.Remove(key);
            }

            private void AssignAction(T instance, SubmitAction action)
            {
                TrackedItem ti;
                _tracked.TryGetValue(instance, out ti);

                switch (action)
                {
                    case SubmitAction.Insert:
                    case SubmitAction.InsertOrUpdate:
                    case SubmitAction.Update:
                    case SubmitAction.Delete:
                    case SubmitAction.None:
                        _tracked[instance] = new TrackedItem(this, instance, ti != null ? ti.Original : null, action, ti != null ? ti.HookedEvent : false);
                        break;
                    case SubmitAction.PossibleUpdate:
                        var notify = instance as INotifyPropertyChanging;
                        if (notify != null)
                        {
                            if (!ti.HookedEvent)
                            {
                                notify.PropertyChanging += OnPropertyChanging;
                            }
                            _tracked[instance] = new TrackedItem(this, instance, null, SubmitAction.PossibleUpdate, true);
                        }
                        else
                        {
                            var original = Mapping.CloneEntity(Entity, instance);
                            _tracked[instance] = new TrackedItem(this, instance, original, SubmitAction.PossibleUpdate, false);
                        }
                        break;
                    default:
                        throw new InvalidOperationException(string.Format("Unknown SubmitAction: {0}", action));
                }
            }

            protected virtual void OnPropertyChanging(object sender, PropertyChangingEventArgs args)
            {
                TrackedItem ti;
                if (_tracked.TryGetValue((T)sender, out ti) && ti.State == SubmitAction.PossibleUpdate)
                {
                    var clone = Mapping.CloneEntity(ti.Entity, ti.Instance);
                    _tracked[(T)sender] = new TrackedItem(this, ti.Instance, clone, SubmitAction.Update, true);
                }
            }
        }

        private class TrackedItem
        {
            internal TrackedItem(ITrackedTable table, object instance, object original, SubmitAction state, bool hookedEvent)
            {
                Table = table;
                Instance = instance;
                Original = original;
                State = state;
                HookedEvent = hookedEvent;
            }

            public ITrackedTable Table { get; }

            public MappingEntity Entity => Table.Entity;

            public object Instance { get; }

            public object Original { get; }

            public SubmitAction State { get; }

            public bool HookedEvent { get; }

            public static readonly IEnumerable<TrackedItem> EmptyList = new TrackedItem[] { };
        }

        private IEnumerable<TrackedItem> GetOrderedItems()
        {
            var items = (from tab in GetTables()
                         from ti in ((ITrackedTable)tab).TrackedItems
                         where ti.State != SubmitAction.None
                         select ti).ToList();

            // build edge maps to represent all references between entities
            var edges = GetEdges(items).Distinct().ToList();
            var stLookup = edges.ToLookup(e => e.Source, e => e.Target);
            var tsLookup = edges.ToLookup(e => e.Target, e => e.Source);

            return items.Sort(item =>
            {
                switch (item.State)
                {
                    case SubmitAction.Insert:
                    case SubmitAction.InsertOrUpdate:
                        // all things this instance depends on must come first
                        var beforeMe = stLookup[item];

                        // if another object exists with same key that is being deleted, then the delete must come before the insert
                        var cached = item.Table.GetFromCacheById(_provider.Mapping.GetPrimaryKey(item.Entity, item.Instance));
                        if (cached != null && cached != item.Instance)
                        {
                            var ti = item.Table.GetTrackedItem(cached);
                            if (ti != null && ti.State == SubmitAction.Delete)
                            {
                                beforeMe = beforeMe.Concat(new[] { ti });
                            }
                        }
                        return beforeMe;

                    case SubmitAction.Delete:
                        // all things that depend on this instance must come first
                        return tsLookup[item];
                    default:
                        return TrackedItem.EmptyList;
                }
            });
        }

        private TrackedItem GetTrackedItem(EntityInfo entity)
        {
            var table = (ITrackedTable)GetTable(entity.Mapping);
            return table.GetTrackedItem(entity.Instance);
        }

        private IEnumerable<Edge> GetEdges(IEnumerable<TrackedItem> items)
        {
            foreach (var c in items)
            {
                foreach (var d in _provider.Mapping.GetDependingEntities(c.Entity, c.Instance))
                {
                    var dc = GetTrackedItem(d);
                    if (dc != null)
                    {
                        yield return new Edge(dc, c);
                    }
                }
                foreach (var d in _provider.Mapping.GetDependentEntities(c.Entity, c.Instance))
                {
                    var dc = GetTrackedItem(d);
                    if (dc != null)
                    {
                        yield return new Edge(c, dc);
                    }
                }
            }
        }

        private class Edge : IEquatable<Edge>
        {
            internal TrackedItem Source { get; private set; }
            internal TrackedItem Target { get; private set; }
            private readonly int _hash;

            internal Edge(TrackedItem source, TrackedItem target)
            {
                Source = source;
                Target = target;
                _hash = Source.GetHashCode() + Target.GetHashCode();
            }

            public bool Equals(Edge edge)
            {
                return edge != null && Source == edge.Source && Target == edge.Target;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as Edge);
            }

            public override int GetHashCode()
            {
                return _hash;
            }
        }
    }
}