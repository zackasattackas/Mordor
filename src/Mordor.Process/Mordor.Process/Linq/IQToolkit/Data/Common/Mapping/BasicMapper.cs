﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Mordor.Process.Linq.IQToolkit.Data.Common.Expressions;
using Mordor.Process.Linq.IQToolkit.Data.Common.Language;
using Mordor.Process.Linq.IQToolkit.Data.Common.Translation;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Mapping
{
    public class BasicMapper : QueryMapper
    {
        private readonly BasicMapping _mapping;
        private readonly QueryTranslator _translator;

        public BasicMapper(BasicMapping mapping, QueryTranslator translator)
        {
            _mapping = mapping;
            _translator = translator;
        }

        public override QueryMapping Mapping => _mapping;

        public override QueryTranslator Translator => _translator;

        /// <summary>
        /// The query language specific type for the column
        /// </summary>
        public virtual QueryType GetColumnType(MappingEntity entity, MemberInfo member)
        {
            var dbType = _mapping.GetColumnDbType(entity, member);
            if (dbType != null)
            {
                return _translator.Linguist.Language.TypeSystem.Parse(dbType);
            }
            return _translator.Linguist.Language.TypeSystem.GetColumnType(TypeHelper.GetMemberType(member));
        }

        public override ProjectionExpression GetQueryExpression(MappingEntity entity)
        {
            var tableAlias = new TableAlias();
            var selectAlias = new TableAlias();
            var table = new TableExpression(tableAlias, entity, _mapping.GetTableName(entity));

            Expression projector = GetEntityExpression(table, entity);
            var pc = ColumnProjector.ProjectColumns(_translator.Linguist.Language, projector, null, selectAlias, tableAlias);

            var proj = new ProjectionExpression(
                new SelectExpression(selectAlias, pc.Columns, table, null),
                pc.Projector
            );

            return (ProjectionExpression)Translator.Police.ApplyPolicy(proj, entity.ElementType);
        }

        public override EntityExpression GetEntityExpression(Expression root, MappingEntity entity)
        {
            // must be some complex type constructed from multiple columns
            var assignments = new List<EntityAssignment>();
            foreach (var mi in _mapping.GetMappedMembers(entity))
            {
                if (!_mapping.IsAssociationRelationship(entity, mi))
                {
                    var me = GetMemberExpression(root, entity, mi);
                    if (me != null)
                    {
                        assignments.Add(new EntityAssignment(mi, me));
                    }
                }
            }

            return new EntityExpression(entity, BuildEntityExpression(entity, assignments));
        }

        public class EntityAssignment
        {
            public MemberInfo Member { get; private set; }
            public Expression Expression { get; private set; }
            public EntityAssignment(MemberInfo member, Expression expression)
            {
                Member = member;
                Debug.Assert(expression != null);
                Expression = expression;
            }
        }

        protected virtual Expression BuildEntityExpression(MappingEntity entity, IList<EntityAssignment> assignments)
        {
            NewExpression newExpression;

            // handle cases where members are not directly assignable
            var readonlyMembers = assignments.Where(b => TypeHelper.IsReadOnly(b.Member)).ToArray();
            var cons = entity.EntityType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            var hasNoArgConstructor = cons.Any(c => c.GetParameters().Length == 0);

            if (readonlyMembers.Length > 0 || !hasNoArgConstructor)
            {
                // find all the constructors that bind all the read-only members
                var consThatApply = cons.Select(c => BindConstructor(c, readonlyMembers))
                    .Where(cbr => cbr != null && cbr.Remaining.Count == 0).ToList();
                if (consThatApply.Count == 0)
                {
                    throw new InvalidOperationException(string.Format("Cannot construct type '{0}' with all mapped includedMembers.", entity.ElementType));
                }
                // just use the first one... (Note: need better algorithm. :-)
                if (readonlyMembers.Length == assignments.Count)
                {
                    return consThatApply[0].Expression;
                }
                var r = BindConstructor(consThatApply[0].Expression.Constructor, assignments);

                newExpression = r.Expression;
                assignments = r.Remaining;
            }
            else
            {
                newExpression = Expression.New(entity.EntityType);
            }

            Expression result;
            if (assignments.Count > 0)
            {
                if (entity.ElementType.IsInterface)
                {
                    assignments = MapAssignments(assignments, entity.EntityType).ToList();
                }
                result = Expression.MemberInit(newExpression, assignments.Select(a => Expression.Bind(a.Member, a.Expression)).ToArray());
            }
            else
            {
                result = newExpression;
            }

            if (entity.ElementType != entity.EntityType)
            {
                result = Expression.Convert(result, entity.ElementType);
            }

            return result;
        }

        private IEnumerable<EntityAssignment> MapAssignments(IEnumerable<EntityAssignment> assignments, Type entityType)
        {
            foreach (var assign in assignments)
            {
                var members = entityType.GetMember(assign.Member.Name, BindingFlags.Instance|BindingFlags.Public);
                if (members != null && members.Length > 0)
                {
                    yield return new EntityAssignment(members[0], assign.Expression);
                }
                else
                {
                    yield return assign;
                }
            }
        }

        protected virtual ConstructorBindResult BindConstructor(ConstructorInfo cons, IList<EntityAssignment> assignments)
        {
            var ps = cons.GetParameters();
            var args = new Expression[ps.Length];
            var mis = new MemberInfo[ps.Length];
            var members = new HashSet<EntityAssignment>(assignments);
            var used = new HashSet<EntityAssignment>();

            for (int i = 0, n = ps.Length; i < n; i++)
            {
                var p = ps[i];
                var assignment = members.FirstOrDefault(a =>
                    p.Name == a.Member.Name
                    && p.ParameterType.IsAssignableFrom(a.Expression.Type));
                if (assignment == null)
                {
                    assignment = members.FirstOrDefault(a =>
                        string.Compare(p.Name, a.Member.Name, true) == 0
                        && p.ParameterType.IsAssignableFrom(a.Expression.Type));
                }
                if (assignment != null)
                {
                    args[i] = assignment.Expression;
                    if (mis != null)
                        mis[i] = assignment.Member;
                    used.Add(assignment);
                }
                else
                {
                    var mems = cons.DeclaringType.GetMember(p.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                    if (mems != null && mems.Length > 0)
                    {
                        args[i] = Expression.Constant(TypeHelper.GetDefault(p.ParameterType), p.ParameterType);
                        mis[i] = mems[0];
                    }
                    else
                    {
                        // unknown parameter, does not match any member
                        return null;
                    }
                }
            }

            members.ExceptWith(used);

            return new ConstructorBindResult(Expression.New(cons, args, mis), members);
        }

        protected class ConstructorBindResult
        {
            public NewExpression Expression { get; private set; }
            public ReadOnlyCollection<EntityAssignment> Remaining { get; private set; }
            public ConstructorBindResult(NewExpression expression, IEnumerable<EntityAssignment> remaining)
            {
                Expression = expression;
                Remaining = remaining.ToReadOnly();
            }
        }

        public override bool HasIncludedMembers(EntityExpression entity)
        {
            var policy = _translator.Police.Policy;
            foreach (var mi in _mapping.GetMappedMembers(entity.Entity))
            {
                if (policy.IsIncluded(mi))
                    return true;
            }
            return false;
        }

        public override EntityExpression IncludeMembers(EntityExpression entity, Func<MemberInfo, bool> fnIsIncluded)
        {
            var assignments = GetAssignments(entity.Expression).ToDictionary(ma => ma.Member.Name);
            var anyAdded = false;
            foreach (var mi in _mapping.GetMappedMembers(entity.Entity))
            {
                EntityAssignment ea;
                var okayToInclude = !assignments.TryGetValue(mi.Name, out ea) || IsNullRelationshipAssignment(entity.Entity, ea);
                if (okayToInclude && fnIsIncluded(mi))
                {
                    ea = new EntityAssignment(mi, GetMemberExpression(entity.Expression, entity.Entity, mi));
                    assignments[mi.Name] = ea;
                    anyAdded = true;
                }
            }
            if (anyAdded)
            {
                return new EntityExpression(entity.Entity, BuildEntityExpression(entity.Entity, assignments.Values.ToList()));
            }
            return entity;
        }

        private bool IsNullRelationshipAssignment(MappingEntity entity, EntityAssignment assignment)
        {
            if (_mapping.IsRelationship(entity, assignment.Member))
            {
                var cex = assignment.Expression as ConstantExpression;
                if (cex != null && cex.Value == null)
                    return true;
            }
            return false;
        }


        private IEnumerable<EntityAssignment> GetAssignments(Expression newOrMemberInit)
        {
            var assignments = new List<EntityAssignment>();
            var minit = newOrMemberInit as MemberInitExpression;
            if (minit != null)
            {
                assignments.AddRange(minit.Bindings.OfType<MemberAssignment>().Select(a => new EntityAssignment(a.Member, a.Expression)));
                newOrMemberInit = minit.NewExpression;
            }
            var nex = newOrMemberInit as NewExpression;
            if (nex != null && nex.Members != null)
            {
                assignments.AddRange(
                    Enumerable.Range(0, nex.Arguments.Count)
                        .Where(i => nex.Members[i] != null)
                        .Select(i => new EntityAssignment(nex.Members[i], nex.Arguments[i]))
                );
            }
            return assignments;
        }


        public override Expression GetMemberExpression(Expression root, MappingEntity entity, MemberInfo member)
        {
            if (_mapping.IsAssociationRelationship(entity, member))
            {
                var relatedEntity = _mapping.GetRelatedEntity(entity, member);
                var projection = GetQueryExpression(relatedEntity);

                // make where clause for joining back to 'root'
                var declaredTypeMembers = _mapping.GetAssociationKeyMembers(entity, member).ToList();
                var associatedMembers = _mapping.GetAssociationRelatedKeyMembers(entity, member).ToList();

                Expression where = null;
                for (int i = 0, n = associatedMembers.Count; i < n; i++)
                {
                    var equal = 
                        GetMemberExpression(projection.Projector, relatedEntity, associatedMembers[i]).Equal(
                            GetMemberExpression(root, entity, declaredTypeMembers[i])
                        );
                    where = (where != null) ? where.And(equal) : equal;
                }

                var newAlias = new TableAlias();
                var pc = ColumnProjector.ProjectColumns(_translator.Linguist.Language, projection.Projector, null, newAlias, projection.Select.Alias);

                var aggregator = Aggregator.GetAggregator(TypeHelper.GetMemberType(member), typeof(IEnumerable<>).MakeGenericType(pc.Projector.Type));
                var result = new ProjectionExpression(
                    new SelectExpression(newAlias, pc.Columns, projection.Select, where),
                    pc.Projector, aggregator
                );

                return _translator.Police.ApplyPolicy(result, member);
            }

            var aliasedRoot = root as AliasedExpression;
            if (aliasedRoot != null && _mapping.IsColumn(entity, member))
            {
                return new ColumnExpression(TypeHelper.GetMemberType(member), GetColumnType(entity, member), aliasedRoot.Alias, _mapping.GetColumnName(entity, member));
            }
            return QueryBinder.BindMember(root, member);
        }

        public override Expression GetInsertExpression(MappingEntity entity, Expression instance, LambdaExpression selector)
        {
            var tableAlias = new TableAlias();
            var table = new TableExpression(tableAlias, entity, _mapping.GetTableName(entity));
            var assignments = GetColumnAssignments(table, instance, entity, (e, m) => !(_mapping.IsGenerated(e, m) || _mapping.IsReadOnly(e,m)));   // #MLCHANGE

            if (selector != null)
            {
                return new BlockCommand(
                    new InsertCommand(table, assignments),
                    GetInsertResult(entity, instance, selector, null)
                );
            }

            return new InsertCommand(table, assignments);
        }

        private IEnumerable<ColumnAssignment> GetColumnAssignments(Expression table, Expression instance, MappingEntity entity, Func<MappingEntity, MemberInfo, bool> fnIncludeColumn)
        {
            foreach (var m in _mapping.GetMappedMembers(entity))
            {
                if (_mapping.IsColumn(entity, m) && fnIncludeColumn(entity, m))
                {
                    yield return new ColumnAssignment(
                        (ColumnExpression)GetMemberExpression(table, entity, m),
                        Expression.MakeMemberAccess(instance, m)
                    );
                }
            }
        }

        protected virtual Expression GetInsertResult(MappingEntity entity, Expression instance, LambdaExpression selector, Dictionary<MemberInfo, Expression> map)
        {
            var tableAlias = new TableAlias();
            var tex = new TableExpression(tableAlias, entity, _mapping.GetTableName(entity));
            var aggregator = Aggregator.GetAggregator(selector.Body.Type, typeof(IEnumerable<>).MakeGenericType(selector.Body.Type));

            Expression where;
            DeclarationCommand genIdCommand = null;
            var generatedIds = _mapping.GetMappedMembers(entity).Where(m => _mapping.IsPrimaryKey(entity, m) && _mapping.IsGenerated(entity, m)).ToList();
            if (generatedIds.Count > 0)
            {
                if (map == null || !generatedIds.Any(m => map.ContainsKey(m)))
                {
                    var localMap = new Dictionary<MemberInfo, Expression>();
                    genIdCommand = GetGeneratedIdCommand(entity, generatedIds.ToList(), localMap);
                    map = localMap;
                }

                // is this just a retrieval of one generated id member?
                var mex = selector.Body as MemberExpression;
                if (mex != null && _mapping.IsPrimaryKey(entity, mex.Member) && _mapping.IsGenerated(entity, mex.Member))
                {
                    if (genIdCommand != null)
                    {
                        // just use the select from the genIdCommand
                        return new ProjectionExpression(
                            genIdCommand.Source,
                            new ColumnExpression(mex.Type, genIdCommand.Variables[0].QueryType, genIdCommand.Source.Alias, genIdCommand.Source.Columns[0].Name),
                            aggregator
                        );
                    }

                    var alias = new TableAlias();
                    var colType = GetColumnType(entity, mex.Member);
                    return new ProjectionExpression(
                        new SelectExpression(alias, new[] { new ColumnDeclaration("", map[mex.Member], colType) }, null, null),
                        new ColumnExpression(TypeHelper.GetMemberType(mex.Member), colType, alias, ""),
                        aggregator
                    );
                }

                where = generatedIds.Select((m, i) =>
                    GetMemberExpression(tex, entity, m).Equal(map[m])
                ).Aggregate((x, y) => x.And(y));
            }
            else
            {
                where = GetIdentityCheck(tex, entity, instance);
            }

            Expression typeProjector = GetEntityExpression(tex, entity);
            var selection = DbExpressionReplacer.Replace(selector.Body, selector.Parameters[0], typeProjector);
            var newAlias = new TableAlias();
            var pc = ColumnProjector.ProjectColumns(_translator.Linguist.Language, selection, null, newAlias, tableAlias);
            var pe = new ProjectionExpression(
                new SelectExpression(newAlias, pc.Columns, tex, where),
                pc.Projector,
                aggregator
            );

            if (genIdCommand != null)
            {
                return new BlockCommand(genIdCommand, pe);
            }
            return pe;
        }

        protected virtual DeclarationCommand GetGeneratedIdCommand(MappingEntity entity, List<MemberInfo> members, Dictionary<MemberInfo, Expression> map)
        {
            var columns = new List<ColumnDeclaration>();
            var decls = new List<VariableDeclaration>();
            var alias = new TableAlias();
            foreach (var member in members)
            {
                var genId = _translator.Linguist.Language.GetGeneratedIdExpression(member);
                var name = member.Name;
                var colType = GetColumnType(entity, member);
                columns.Add(new ColumnDeclaration(member.Name, genId, colType));
                decls.Add(new VariableDeclaration(member.Name, colType, new ColumnExpression(genId.Type, colType, alias, member.Name)));
                if (map != null)
                {
                    var vex = new VariableExpression(member.Name, TypeHelper.GetMemberType(member), colType);
                    map.Add(member, vex);
                }
            }
            var select = new SelectExpression(alias, columns, null, null);
            return new DeclarationCommand(decls, select);
        }

        protected virtual Expression GetIdentityCheck(Expression root, MappingEntity entity, Expression instance)
        {
            return _mapping.GetMappedMembers(entity)
                .Where(m => _mapping.IsPrimaryKey(entity, m))
                .Select(m => GetMemberExpression(root, entity, m).Equal(Expression.MakeMemberAccess(instance, m)))
                .Aggregate((x, y) => x.And(y));
        }

        protected virtual Expression GetEntityExistsTest(MappingEntity entity, Expression instance)
        {
            var tq = GetQueryExpression(entity);
            var where = GetIdentityCheck(tq.Select, entity, instance);
            return new ExistsExpression(new SelectExpression(new TableAlias(), null, tq.Select, where));
        }

        protected virtual Expression GetEntityStateTest(MappingEntity entity, Expression instance, LambdaExpression updateCheck)
        {
            var tq = GetQueryExpression(entity);
            var where = GetIdentityCheck(tq.Select, entity, instance);
            var check = DbExpressionReplacer.Replace(updateCheck.Body, updateCheck.Parameters[0], tq.Projector);
            where = where.And(check);
            return new ExistsExpression(new SelectExpression(new TableAlias(), null, tq.Select, where));
        }

        public override Expression GetUpdateExpression(MappingEntity entity, Expression instance, LambdaExpression updateCheck, LambdaExpression selector, Expression @else)
        {
            var tableAlias = new TableAlias();
            var table = new TableExpression(tableAlias, entity, _mapping.GetTableName(entity));

            var where = GetIdentityCheck(table, entity, instance);
            if (updateCheck != null)
            {
                Expression typeProjector = GetEntityExpression(table, entity);
                var pred = DbExpressionReplacer.Replace(updateCheck.Body, updateCheck.Parameters[0], typeProjector);
                where = where.And(pred);
            }

            var assignments = GetColumnAssignments(table, instance, entity, (e, m) => _mapping.IsUpdatable(e, m));

            Expression update = new UpdateCommand(table, where, assignments);

            if (selector != null)
            {
                return new BlockCommand(
                    update,
                    new IfCommand(
                        _translator.Linguist.Language.GetRowsAffectedExpression(update).GreaterThan(Expression.Constant(0)),
                        GetUpdateResult(entity, instance, selector),
                        @else
                    )
                );
            }

            if (@else != null)
            {
                return new BlockCommand(
                    update,
                    new IfCommand(
                        _translator.Linguist.Language.GetRowsAffectedExpression(update).LessThanOrEqual(Expression.Constant(0)),
                        @else,
                        null
                    )
                );
            }

            return update;
        }

        protected virtual Expression GetUpdateResult(MappingEntity entity, Expression instance, LambdaExpression selector)
        {
            var tq = GetQueryExpression(entity);
            var where = GetIdentityCheck(tq.Select, entity, instance);
            var selection = DbExpressionReplacer.Replace(selector.Body, selector.Parameters[0], tq.Projector);
            var newAlias = new TableAlias();
            var pc = ColumnProjector.ProjectColumns(_translator.Linguist.Language, selection, null, newAlias, tq.Select.Alias);
            return new ProjectionExpression(
                new SelectExpression(newAlias, pc.Columns, tq.Select, where),
                pc.Projector,
                Aggregator.GetAggregator(selector.Body.Type, typeof(IEnumerable<>).MakeGenericType(selector.Body.Type))
            );
        }

        public override Expression GetInsertOrUpdateExpression(MappingEntity entity, Expression instance, LambdaExpression updateCheck, LambdaExpression resultSelector)
        {
            if (updateCheck != null)
            {
                var insert = GetInsertExpression(entity, instance, resultSelector);
                var update = GetUpdateExpression(entity, instance, updateCheck, resultSelector, null);
                var check = GetEntityExistsTest(entity, instance);
                return new IfCommand(check, update, insert);
            }
            else 
            {
                var insert = GetInsertExpression(entity, instance, resultSelector);
                var update = GetUpdateExpression(entity, instance, updateCheck, resultSelector, insert);
                return update;
            }
        }

        public override Expression GetDeleteExpression(MappingEntity entity, Expression instance, LambdaExpression deleteCheck)
        {
            var table = new TableExpression(new TableAlias(), entity, _mapping.GetTableName(entity));
            Expression where = null;

            if (instance != null)
            {
                where = GetIdentityCheck(table, entity, instance);
            }

            if (deleteCheck != null)
            {
                Expression row = GetEntityExpression(table, entity);
                var pred = DbExpressionReplacer.Replace(deleteCheck.Body, deleteCheck.Parameters[0], row);
                where = (where != null) ? where.And(pred) : pred;
            }

            return new DeleteCommand(table, where);
        }
    }
}