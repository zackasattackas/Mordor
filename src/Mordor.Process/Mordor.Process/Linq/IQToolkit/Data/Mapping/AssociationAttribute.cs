using System;

namespace Mordor.Process.Linq.IQToolkit.Data.Mapping
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class AssociationAttribute : MemberAttribute
    {
        public string Name { get; set; }
        public string KeyMembers { get; set; }
        public string RelatedEntityId { get; set; }
        public Type RelatedEntityType { get; set; }
        public string RelatedKeyMembers { get; set; }
        public bool IsForeignKey { get; set; }
    }
}