using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class FileNameConfig
    {
        [JsonProperty("structure")]
        public StructureConfig Structure { get; set; }

        [JsonProperty("parts")]
        public List<PartConfig> Parts { get; set; }
    }

    public class StructureConfig
    {
        [JsonProperty("separator")]
        public string Separator { get; set; }

        [JsonProperty("totalParts")]
        public int TotalParts { get; set; }

        [JsonProperty("considerSpaces")]
        public bool ConsiderSpaces { get; set; }
    }

    public class PartConfig
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("position")]
        public int Position { get; set; }

        [JsonProperty("allowedValues")]
        public List<AllowedValue> AllowedValues { get; set; }

        [JsonProperty("required")]
        public bool Required { get; set; }
    }

    public class AllowedValue
    {
        [JsonProperty("Code")]
        public string Code { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }
    }
}
