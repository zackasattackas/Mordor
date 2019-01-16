using System;

namespace Mordor.Process.Internal
{
    [AttributeUsage(AttributeTargets.Property)]
    public class CimInstanceProperty : Attribute
    {
        public string Name { get; }

        public CimInstanceProperty()
        {            
        }

        public CimInstanceProperty(string name)
        {
            Name = name;
        }
    }
}