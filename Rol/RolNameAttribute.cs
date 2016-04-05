using System;

namespace Rol
{
    public class RolNameAttribute : Attribute
    {
        public string Name { get; set; }
        public RolNameAttribute(string name)
        {
            Name = name;
        }
    }
}