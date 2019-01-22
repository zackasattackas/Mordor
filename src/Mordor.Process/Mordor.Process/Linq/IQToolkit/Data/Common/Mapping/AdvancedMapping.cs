// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System.Collections.Generic;
using System.Reflection;

namespace Mordor.Process.Linq.IQToolkit.Data.Common.Mapping
{
    public abstract class AdvancedMapping : BasicMapping
    {
        public abstract bool IsNestedEntity(MappingEntity entity, MemberInfo member);
        public abstract IList<MappingTable> GetTables(MappingEntity entity);
        public abstract string GetAlias(MappingTable table);
        public abstract string GetAlias(MappingEntity entity, MemberInfo member);
        public abstract string GetTableName(MappingTable table);
        public abstract bool IsExtensionTable(MappingTable table);
        public abstract string GetExtensionRelatedAlias(MappingTable table);
        public abstract IEnumerable<string> GetExtensionKeyColumnNames(MappingTable table);
        public abstract IEnumerable<MemberInfo> GetExtensionRelatedMembers(MappingTable table);

        public override bool IsRelationship(MappingEntity entity, MemberInfo member)
        {
            return base.IsRelationship(entity, member)
                || IsNestedEntity(entity, member);
        }

        public override object CloneEntity(MappingEntity entity, object instance)
        {
            var clone = base.CloneEntity(entity, instance);

            // need to clone nested entities too
            foreach (var mi in GetMappedMembers(entity))
            {
                if (IsNestedEntity(entity, mi))
                {
                    var nested = GetRelatedEntity(entity, mi);
                    var nestedValue = mi.GetValue(instance);
                    if (nestedValue != null)
                    {
                        var nestedClone = CloneEntity(nested, mi.GetValue(instance));
                        mi.SetValue(clone, nestedClone);
                    }
                }
            }

            return clone;
        }

        public override bool IsModified(MappingEntity entity, object instance, object original)
        {
            if (base.IsModified(entity, instance, original))
                return true;

            // need to check nested entities too
            foreach (var mi in GetMappedMembers(entity))
            {
                if (IsNestedEntity(entity, mi))
                {
                    var nested = GetRelatedEntity(entity, mi);
                    if (IsModified(nested, mi.GetValue(instance), mi.GetValue(original)))
                        return true;
                }
            }

            return false;
        }

        public override QueryMapper CreateMapper(QueryTranslator translator)
        {
            return new AdvancedMapper(this, translator);
        }
    }
}