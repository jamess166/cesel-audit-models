using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class ParameterDefinition
    {
        public string Name { get; set; }
        public string ParameterType { get; set; }
        public string Group { get; set; }
        public string Scope { get; set; }
        public bool Required { get; set; } = false;
    }

    public class SpecialtyParameters
    {
        public string Specialty { get; set; }
        public string Prefix { get; set; }
        public List<ParameterDefinition> Parameters { get; set; }
    }

    public class ParameterDefinitionComparer : IEqualityComparer<ParameterDefinition>
    {
        public bool Equals(ParameterDefinition x, ParameterDefinition y)
        {
            if (x == null || y == null)
                return false;

            return x.Name == y.Name &&
                   x.ParameterType == y.ParameterType &&
                   x.Group == y.Group &&
                   x.Scope == y.Scope;
        }

        public int GetHashCode(ParameterDefinition obj)
        {
            return (obj.Name + obj.ParameterType + obj.Group + obj.Scope).GetHashCode();
        }
    }

}
