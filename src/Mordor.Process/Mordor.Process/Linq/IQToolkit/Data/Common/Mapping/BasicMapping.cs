﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Mapping
{
    public abstract class BasicMapping : QueryMapping
    {
        public override MappingEntity GetEntity(Type elementType, string tableId)
        {
            if (tableId == null)
                tableId = GetTableId(elementType);
            return new BasicMappingEntity(elementType, tableId);
        }

        public override MappingEntity GetEntity(MemberInfo contextMember)
        {
            var elementType = TypeHelper.GetElementType(TypeHelper.GetMemberType(contextMember));
            return GetEntity(elementType);
        }

        private class BasicMappingEntity : MappingEntity
        {
            private readonly Type _type;

            public BasicMappingEntity(Type type, string entityId)
            {
                TableId = entityId;
                _type = type;
            }

            public override string TableId { get; }

            public override Type ElementType => _type;

            public override Type EntityType => _type;
        }

        public override bool IsRelationship(MappingEntity entity, MemberInfo member)
        {
            return IsAssociationRelationship(entity, member);
        }

        /// <summary>
        /// Deterimines is a property is mapped onto a column or relationship
        /// </summary>
        public virtual bool IsMapped(MappingEntity entity, MemberInfo member)
        {
            return true;
        }

        /// <summary>
        /// Determines if a property is mapped onto a column
        /// </summary>
        public virtual bool IsColumn(MappingEntity entity, MemberInfo member)
        {
            //return this.mapping.IsMapped(entity, member) && this.translator.Linguist.Language.IsScalar(TypeHelper.GetMemberType(member));
            return IsMapped(entity, member);
        }

        /// <summary>
        /// The type declaration for the column in the provider's syntax
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="member"></param>
        /// <returns>a string representing the type declaration or null</returns>
        public virtual string GetColumnDbType(MappingEntity entity, MemberInfo member)
        {
            return null;
        }

        /// <summary>
        /// Determines if a property represents or is part of the entities unique identity (often primary key)
        /// </summary>
        public override bool IsPrimaryKey(MappingEntity entity, MemberInfo member)
        {
            return false;
        }

        /// <summary>
        /// Determines if a property is computed after insert or update
        /// </summary>
        public virtual bool IsComputed(MappingEntity entity, MemberInfo member)
        {
            return false;
        }

        /// <summary>
        /// Determines if a property is generated on the server during insert
        /// </summary>
        public virtual bool IsGenerated(MappingEntity entity, MemberInfo member)
        {
            return false;
        }
        
        /// <summary>
        /// Determines if a property should not be written back to database
        /// </summary>
        public virtual bool IsReadOnly(MappingEntity entity, MemberInfo member)
        {
            return false;
        }

        /// <summary>
        /// Determines if a property can be part of an update operation
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="member"></param>
        /// <returns></returns>
        public virtual bool IsUpdatable(MappingEntity entity, MemberInfo member)
        {
            return !IsPrimaryKey(entity, member) && !IsReadOnly(entity, member);   
        }

        /// <summary>
        /// The type of the entity on the other side of the relationship
        /// </summary>
        public virtual MappingEntity GetRelatedEntity(MappingEntity entity, MemberInfo member)
        {
            var relatedType = TypeHelper.GetElementType(TypeHelper.GetMemberType(member));
            return GetEntity(relatedType);
        }

        /// <summary>
        /// Determines if the property is an assocation relationship.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="member"></param>
        /// <returns></returns>
        public virtual bool IsAssociationRelationship(MappingEntity entity, MemberInfo member)
        {
            return false;
        }

        /// <summary>
        /// Returns the key members on this side of the association
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="member"></param>
        /// <returns></returns>
        public virtual IEnumerable<MemberInfo> GetAssociationKeyMembers(MappingEntity entity, MemberInfo member)
        {            
            return new MemberInfo[] { };
        }

        /// <summary>
        /// Returns the key members on the other side (related side) of the association
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="member"></param>
        /// <returns></returns>
        public virtual IEnumerable<MemberInfo> GetAssociationRelatedKeyMembers(MappingEntity entity, MemberInfo member)
        {
            return new MemberInfo[] { };
        }

        public abstract bool IsRelationshipSource(MappingEntity entity, MemberInfo member);

        public abstract bool IsRelationshipTarget(MappingEntity entity, MemberInfo member);

        /// <summary>
        /// The name of the corresponding database table
        /// </summary>
        public virtual string GetTableName(MappingEntity entity)
        {
            return entity.EntityType.Name;
        }

        /// <summary>
        /// The name of the corresponding table column
        /// </summary>
        public virtual string GetColumnName(MappingEntity entity, MemberInfo member)
        {
            return member.Name;
        }

        /// <summary>
        /// A sequence of all the mapped members
        /// </summary>
        public override IEnumerable<MemberInfo> GetMappedMembers(MappingEntity entity)
        {
            //Type type = entity.ElementType.IsInterface ? entity.EntityType : entity.ElementType;
            var type = entity.EntityType;
            var members = new HashSet<MemberInfo>(type.GetFields().Cast<MemberInfo>().Where(m => IsMapped(entity, m)));
            members.UnionWith(type.GetProperties().Cast<MemberInfo>().Where(m => IsMapped(entity, m)));
            return members.OrderBy(m => m.Name);
        }

        public override object CloneEntity(MappingEntity entity, object instance)
        {
            var clone = FormatterServices.GetUninitializedObject(entity.EntityType);
            foreach (var mi in GetMappedMembers(entity))
            {
                if (IsColumn(entity, mi))
                {
                    mi.SetValue(clone, mi.GetValue(instance));
                }
            }
            return clone;
        }

        public override bool IsModified(MappingEntity entity, object instance, object original)
        {
            foreach (var mi in GetMappedMembers(entity))
            {
                if (IsColumn(entity, mi))
                {
                    if (!Equals(mi.GetValue(instance), mi.GetValue(original)))
                        return true;
                }
            }
            return false;
        }

        public override object GetPrimaryKey(MappingEntity entity, object instance)
        {
            object firstKey = null;
            List<object> keys = null;
            foreach (var mi in GetPrimaryKeyMembers(entity))
            {
                if (firstKey == null)
                {
                    firstKey = mi.GetValue(instance);
                }
                else
                {
                    if (keys == null)
                    {
                        keys = new List<object>();
                        keys.Add(firstKey);
                    }
                    keys.Add(mi.GetValue(instance));
                }
            }
            if (keys != null)
            {
                return new CompoundKey(keys.ToArray());
            }
            return firstKey;
        }

        public override Expression GetPrimaryKeyQuery(MappingEntity entity, Expression source, Expression[] keys)
        {
            // make predicate
            var p = Expression.Parameter(entity.ElementType, "p");
            Expression pred = null;
            var idMembers = GetPrimaryKeyMembers(entity).ToList();
            if (idMembers.Count != keys.Length)
            {
                throw new InvalidOperationException("Incorrect number of primary key values");
            }
            for (int i = 0, n = keys.Length; i < n; i++)
            {
                var mem = idMembers[i];
                var memberType = TypeHelper.GetMemberType(mem);
                if (keys[i] != null && TypeHelper.GetNonNullableType(keys[i].Type) != TypeHelper.GetNonNullableType(memberType))
                {
                    throw new InvalidOperationException("Primary key value is wrong type");
                }
                var eq = Expression.MakeMemberAccess(p, mem).Equal(keys[i]);
                pred = (pred == null) ? eq : pred.And(eq);
            }
            var predLambda = Expression.Lambda(pred, p);

            return Expression.Call(typeof(Queryable), "SingleOrDefault", new[] { entity.ElementType }, source, predLambda);
        }

        public override IEnumerable<EntityInfo> GetDependentEntities(MappingEntity entity, object instance)
        {
            foreach (var mi in GetMappedMembers(entity))
            {
                if (IsRelationship(entity, mi) && IsRelationshipSource(entity, mi))
                {
                    var relatedEntity = GetRelatedEntity(entity, mi);
                    var value = mi.GetValue(instance);
                    if (value != null)
                    {
                        var list = value as IList;
                        if (list != null)
                        {
                            foreach (var item in list)
                            {
                                if (item != null)
                                {
                                    yield return new EntityInfo(item, relatedEntity);
                                }
                            }
                        }
                        else
                        {
                            yield return new EntityInfo(value, relatedEntity);
                        }
                    }
                }
            }
        }

        public override IEnumerable<EntityInfo> GetDependingEntities(MappingEntity entity, object instance)
        {
            foreach (var mi in GetMappedMembers(entity))
            {
                if (IsRelationship(entity, mi) && IsRelationshipTarget(entity, mi))
                {
                    var relatedEntity = GetRelatedEntity(entity, mi);
                    var value = mi.GetValue(instance);
                    if (value != null)
                    {
                        var list = value as IList;
                        if (list != null)
                        {
                            foreach (var item in list)
                            {
                                if (item != null)
                                {
                                    yield return new EntityInfo(item, relatedEntity);
                                }
                            }
                        }
                        else
                        {
                            yield return new EntityInfo(value, relatedEntity);
                        }
                    }
                }
            }
        }

        public override QueryMapper CreateMapper(QueryTranslator translator)
        {
            return new BasicMapper(this, translator);
        }
    }
}
