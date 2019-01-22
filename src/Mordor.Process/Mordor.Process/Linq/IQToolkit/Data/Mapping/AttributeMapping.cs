// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using Mordor.Process.Linq.IQToolkit.Data.Common;
using Mordor.Process.Linq.IQToolkit.Data.Common.Mapping;

namespace Mordor.Process.Linq.IQToolkit.Data.Mapping
{
    public class AttributeMapping : AdvancedMapping
    {
        private readonly Type _contextType;
        private readonly Dictionary<string, MappingEntity> _entities = new Dictionary<string, MappingEntity>();
        private readonly ReaderWriterLock _rwLock = new ReaderWriterLock();

        public AttributeMapping(Type contextType)
        {
            _contextType = contextType;
        }

        public override MappingEntity GetEntity(MemberInfo contextMember)
        {
            var elementType = TypeHelper.GetElementType(TypeHelper.GetMemberType(contextMember));
            return GetEntity(elementType, contextMember.Name);
        }

        public override MappingEntity GetEntity(Type type, string tableId)
        {
            return GetEntity(type, tableId, type);
        }

        private MappingEntity GetEntity(Type elementType, string tableId, Type entityType)
        {
            MappingEntity entity;
            _rwLock.AcquireReaderLock(Timeout.Infinite);
            if (!_entities.TryGetValue(tableId, out entity))
            {
                _rwLock.ReleaseReaderLock();
                _rwLock.AcquireWriterLock(Timeout.Infinite);
                if (!_entities.TryGetValue(tableId, out entity))
                {
                    entity = CreateEntity(elementType, tableId, entityType);
                    _entities.Add(tableId, entity);
                }
                _rwLock.ReleaseWriterLock();
            }
            else
            {
                _rwLock.ReleaseReaderLock();
            }
            return entity;
        }

        protected virtual IEnumerable<MappingAttribute> GetMappingAttributes(string rootEntityId)
        {
            var contextMember = FindMember(_contextType, rootEntityId);
            return (MappingAttribute[])Attribute.GetCustomAttributes(contextMember, typeof(MappingAttribute));
        }

        public override string GetTableId(Type entityType)
        {
            if (_contextType != null)
            {
                foreach (var mi in _contextType.GetMembers(BindingFlags.Instance | BindingFlags.Public))
                {
                    var fi = mi as FieldInfo;
                    if (fi != null && TypeHelper.GetElementType(fi.FieldType) == entityType)
                        return fi.Name;
                    var pi = mi as PropertyInfo;
                    if (pi != null && TypeHelper.GetElementType(pi.PropertyType) == entityType)
                        return pi.Name;
                }
            }
            return entityType.Name;
        }

        private MappingEntity CreateEntity(Type elementType, string tableId, Type entityType)
        {
            if (tableId == null)
                tableId = GetTableId(elementType);
            var members = new HashSet<string>();
            var mappingMembers = new List<AttributeMappingMember>();
            var dot = tableId.IndexOf('.');
            var rootTableId = dot > 0 ? tableId.Substring(0, dot) : tableId;
            var path = dot > 0 ? tableId.Substring(dot + 1) : "";
            var mappingAttributes = GetMappingAttributes(rootTableId);
            var tableAttributes = mappingAttributes.OfType<TableBaseAttribute>()
                .OrderBy(ta => ta.Name);
            var tableAttr = tableAttributes.OfType<TableAttribute>().FirstOrDefault();
            if (tableAttr != null && tableAttr.EntityType != null && entityType == elementType)
            {
                entityType = tableAttr.EntityType;
            }
            var memberAttributes = mappingAttributes.OfType<MemberAttribute>()
                .Where(ma => ma.Member.StartsWith(path))
                .OrderBy(ma => ma.Member);

            foreach (var attr in memberAttributes)
            {
                if (string.IsNullOrEmpty(attr.Member))
                    continue;
                var memberName = (path.Length == 0) ? attr.Member : attr.Member.Substring(path.Length + 1);
                MemberInfo member = null;
                MemberAttribute attribute = null;
                AttributeMappingEntity nested = null;
                if (memberName.Contains('.')) // additional nested mappings
                {
                    var nestedMember = memberName.Substring(0, memberName.IndexOf('.'));
                    if (nestedMember.Contains('.'))
                        continue; // don't consider deeply nested members yet
                    if (members.Contains(nestedMember))
                        continue; // already seen it (ignore additional)
                    members.Add(nestedMember);
                    member = FindMember(entityType, nestedMember);
                    var newTableId = tableId + "." + nestedMember;
                    nested = (AttributeMappingEntity)GetEntity(TypeHelper.GetMemberType(member), newTableId);
                }
                else 
                {
                    if (members.Contains(memberName))
                    {
                        throw new InvalidOperationException(string.Format("AttributeMapping: more than one mapping attribute specified for member '{0}' on type '{1}'", memberName, entityType.Name));
                    }
                    member = FindMember(entityType, memberName);
                    attribute = attr;
                }
                mappingMembers.Add(new AttributeMappingMember(member, attribute, nested));
            }
            return new AttributeMappingEntity(elementType, tableId, entityType, tableAttributes, mappingMembers);
        }

        private static readonly char[] DotSeparator = { '.' };

        private MemberInfo FindMember(Type type, string path)
        {
            MemberInfo member = null;
            var names = path.Split(DotSeparator);
            foreach (var name in names)
            {
                member = type.GetMember(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase).FirstOrDefault();
                if (member == null)
                {
                    throw new InvalidOperationException(string.Format("AttributMapping: the member '{0}' does not exist on type '{1}'", name, type.Name));
                }
                type = TypeHelper.GetElementType(TypeHelper.GetMemberType(member));
            }
            return member;
        }

        public override string GetTableName(MappingEntity entity)
        {
            var en = (AttributeMappingEntity)entity;
            var table = en.Tables.FirstOrDefault();
            return GetTableName(table);
        }

        private string GetTableName(MappingEntity entity, TableBaseAttribute attr)
        {
            var name = (attr != null && !string.IsNullOrEmpty(attr.Name))
                ? attr.Name
                : entity.TableId;
            return name;
        }

        public override IEnumerable<MemberInfo> GetMappedMembers(MappingEntity entity)
        {
            return ((AttributeMappingEntity)entity).MappedMembers;
        }

        public override bool IsMapped(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            return mm != null;
        }

        public override bool IsColumn(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            return mm != null && mm.Column != null;
        }

        public override bool IsComputed(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            return mm != null && mm.Column != null && mm.Column.IsComputed;
        }

        public override bool IsGenerated(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            return mm != null && mm.Column != null && mm.Column.IsGenerated;
        }
        
        public override bool IsReadOnly(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            return mm != null && mm.Column != null && mm.Column.IsReadOnly;
        }

        public override bool IsPrimaryKey(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            return mm != null && mm.Column != null && mm.Column.IsPrimaryKey;
        }

        public override string GetColumnName(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            if (mm != null && mm.Column != null && !string.IsNullOrEmpty(mm.Column.Name))
                return mm.Column.Name;
            return base.GetColumnName(entity, member);
        }

        public override string GetColumnDbType(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            if (mm != null && mm.Column != null && !string.IsNullOrEmpty(mm.Column.DbType))
                return mm.Column.DbType;
            return null;
        }

        public override bool IsAssociationRelationship(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            return mm != null && mm.Association != null;        
        }

        public override bool IsRelationshipSource(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            if (mm != null && mm.Association != null)
            {
                if (mm.Association.IsForeignKey && !typeof(IEnumerable).IsAssignableFrom(TypeHelper.GetMemberType(member)))
                    return true;
            }
            return false;
        }

        public override bool IsRelationshipTarget(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            if (mm != null && mm.Association != null)
            {
                if (!mm.Association.IsForeignKey || typeof(IEnumerable).IsAssignableFrom(TypeHelper.GetMemberType(member)))
                    return true;
            }
            return false;
        }

        public override bool IsNestedEntity(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            return mm != null && mm.NestedEntity != null;
        }

        public override MappingEntity GetRelatedEntity(MappingEntity entity, MemberInfo member)
        {
            var thisEntity = (AttributeMappingEntity)entity;
            var mm = thisEntity.GetMappingMember(member.Name);
            if (mm != null)
            {
                if (mm.Association != null)
                {
                    var elementType = TypeHelper.GetElementType(TypeHelper.GetMemberType(member));
                    var entityType = (mm.Association.RelatedEntityType != null) ? mm.Association.RelatedEntityType : elementType;
                    return GetReferencedEntity(elementType, mm.Association.RelatedEntityId, entityType, "Association.RelatedEntityID");
                }

                if (mm.NestedEntity != null)
                {
                    return mm.NestedEntity;
                }
            }
            return base.GetRelatedEntity(entity, member);
        }

        private static readonly char[] Separators = {' ', ',', '|' };

        public override IEnumerable<MemberInfo> GetAssociationKeyMembers(MappingEntity entity, MemberInfo member)
        {
            var thisEntity = (AttributeMappingEntity)entity;
            var mm = thisEntity.GetMappingMember(member.Name);
            if (mm != null && mm.Association != null)
            {
                return GetReferencedMembers(thisEntity, mm.Association.KeyMembers, "Association.KeyMembers", thisEntity.EntityType);
            }
            return base.GetAssociationKeyMembers(entity, member);
        }

        public override IEnumerable<MemberInfo> GetAssociationRelatedKeyMembers(MappingEntity entity, MemberInfo member)
        {
            var thisEntity = (AttributeMappingEntity)entity;
            var relatedEntity = (AttributeMappingEntity)GetRelatedEntity(entity, member);
            var mm = thisEntity.GetMappingMember(member.Name);
            if (mm != null && mm.Association != null)
            {
                return GetReferencedMembers(relatedEntity, mm.Association.RelatedKeyMembers, "Association.RelatedKeyMembers", thisEntity.EntityType);
            }
            return base.GetAssociationRelatedKeyMembers(entity, member);
        }

        private IEnumerable<MemberInfo> GetReferencedMembers(AttributeMappingEntity entity, string names, string source, Type sourceType)
        {
            return names.Split(Separators).Select(n => GetReferencedMember(entity, n, source, sourceType));
        }

        private MemberInfo GetReferencedMember(AttributeMappingEntity entity, string name, string source, Type sourceType)
        {
            var mm = entity.GetMappingMember(name);
            if (mm == null)
            {
                throw new InvalidOperationException(string.Format("AttributeMapping: The member '{0}.{1}' referenced in {2} for '{3}' is not mapped or does not exist", entity.EntityType.Name, name, source, sourceType.Name));
            }
            return mm.Member;
        }

        private MappingEntity GetReferencedEntity(Type elementType, string name, Type entityType, string source)
        {
            var entity = GetEntity(elementType, name, entityType);
            if (entity == null)
            {
                throw new InvalidOperationException(string.Format("The entity '{0}' referenced in {1} of '{2}' does not exist", name, source, entityType.Name));
            }
            return entity;
        }

        public override IList<MappingTable> GetTables(MappingEntity entity)
        {
            return ((AttributeMappingEntity)entity).Tables;
        }

        public override string GetAlias(MappingTable table)
        {
            return ((AttributeMappingTable)table).Attribute.Alias;
        }

        public override string GetAlias(MappingEntity entity, MemberInfo member)
        {
            var mm = ((AttributeMappingEntity)entity).GetMappingMember(member.Name);
            return (mm != null && mm.Column != null) ? mm.Column.Alias : null;
        }

        public override string GetTableName(MappingTable table)
        {
            var amt = (AttributeMappingTable)table;
            return GetTableName(amt.Entity, amt.Attribute);
        }

        public override bool IsExtensionTable(MappingTable table)
        {
            return ((AttributeMappingTable)table).Attribute is ExtensionTableAttribute;
        }

        public override string GetExtensionRelatedAlias(MappingTable table)
        {
            var attr = ((AttributeMappingTable)table).Attribute as ExtensionTableAttribute;
            return (attr != null) ? attr.RelatedAlias : null;
        }

        public override IEnumerable<string> GetExtensionKeyColumnNames(MappingTable table)
        {
            var attr = ((AttributeMappingTable)table).Attribute as ExtensionTableAttribute;
            if (attr == null) return new string[] { };
            return attr.KeyColumns.Split(Separators);
        }

        public override IEnumerable<MemberInfo> GetExtensionRelatedMembers(MappingTable table)
        {
            var amt = (AttributeMappingTable)table;
            var attr = amt.Attribute as ExtensionTableAttribute;
            if (attr == null) return new MemberInfo[] { };
            return attr.RelatedKeyColumns.Split(Separators).Select(n => GetMemberForColumn(amt.Entity, n));
        }

        private MemberInfo GetMemberForColumn(MappingEntity entity, string columnName)
        {
            foreach (var m in GetMappedMembers(entity))
            {
                if (IsNestedEntity(entity, m))
                {
                    var m2 = GetMemberForColumn(GetRelatedEntity(entity, m), columnName);
                    if (m2 != null)
                        return m2;
                }
                else if (IsColumn(entity, m) && string.Compare(GetColumnName(entity, m), columnName, true) == 0)
                {
                    return m;
                }
            }
            return null;
        }

        public override QueryMapper CreateMapper(QueryTranslator translator)
        {
            return new AttributeMapper(this, translator);
        }

        private class AttributeMapper : AdvancedMapper
        {
            private AttributeMapping _mapping;

            public AttributeMapper(AttributeMapping mapping, QueryTranslator translator)
                : base(mapping, translator)
            {
                _mapping = mapping;
            }
        }

        private class AttributeMappingMember
        {
            private readonly MemberAttribute _attribute;

            internal AttributeMappingMember(MemberInfo member, MemberAttribute attribute, AttributeMappingEntity nested)
            {
                Member = member;
                _attribute = attribute;
                NestedEntity = nested;
            }

            internal MemberInfo Member { get; }

            internal ColumnAttribute Column => _attribute as ColumnAttribute;

            internal AssociationAttribute Association => _attribute as AssociationAttribute;

            internal AttributeMappingEntity NestedEntity { get; }
        }

        private class AttributeMappingTable : MappingTable
        {
            internal AttributeMappingTable(AttributeMappingEntity entity, TableBaseAttribute attribute)
            {
                Entity = entity;
                Attribute = attribute;
            }

            public AttributeMappingEntity Entity { get; }

            public TableBaseAttribute Attribute { get; }
        }

        private class AttributeMappingEntity : MappingEntity
        {
            private readonly Dictionary<string, AttributeMappingMember> _mappingMembers;

            internal AttributeMappingEntity(Type elementType, string tableId, Type entityType, IEnumerable<TableBaseAttribute> attrs, IEnumerable<AttributeMappingMember> mappingMembers)
            {
                TableId = tableId;
                ElementType = elementType;
                EntityType = entityType;
                Tables = attrs.Select(a => (MappingTable)new AttributeMappingTable(this, a)).ToReadOnly();
                _mappingMembers = mappingMembers.ToDictionary(mm => mm.Member.Name);
            }

            public override string TableId { get; }

            public override Type ElementType { get; }

            public override Type EntityType { get; }

            internal ReadOnlyCollection<MappingTable> Tables { get; }

            internal AttributeMappingMember GetMappingMember(string name)
            {
                AttributeMappingMember mm = null;
                _mappingMembers.TryGetValue(name, out mm);
                return mm;
            }

            internal IEnumerable<MemberInfo> MappedMembers
            {
                get { return _mappingMembers.Values.Select(mm => mm.Member); }
            }
        }
    }
}
