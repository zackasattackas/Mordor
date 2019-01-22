using System;

namespace Mordor.Process.Linq.IQToolkit.Data.Mapping
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class ExtensionTableAttribute : TableBaseAttribute
    {
        public string KeyColumns { get; set; }
        public string RelatedAlias { get; set; }
        public string RelatedKeyColumns { get; set; }
    }
}