using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public static class JsonHelper
    {
        public static string GetJsonPath(string modelDirectory)
        {
            // 1. Revisar la carpeta del modelo
            string jsonPath = Path.Combine(modelDirectory, "Data.json");
            if (File.Exists(jsonPath))
                return jsonPath;

            // 2. Revisar la carpeta superior
            string parentDirectory = Directory.GetParent(modelDirectory)?.FullName;
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                string parentJsonPath = Path.Combine(parentDirectory, "Data.json");
                if (File.Exists(parentJsonPath))
                    return parentJsonPath;
            }

            return null; // No encontrado
        }

        public static DeliverableData LoadDeliverable(string modelDirectory)
        {
            string path = GetJsonPath(modelDirectory);
            if (string.IsNullOrEmpty(path))
                return null;

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<DeliverableData>(json);
        }
    }
}
