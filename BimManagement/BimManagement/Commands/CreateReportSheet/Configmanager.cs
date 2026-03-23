using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace BimManagement
{
    [DataContract]
    public class AppConfig
    {
        [DataMember(Name = "poCarValue")]
        public string PoCarValue { get; set; } = "";

        [DataMember(Name = "sharedParameterFilePath")]
        public string SharedParameterFilePath { get; set; } =
            @"C:\ProgramData\Autodesk\Revit\Addins\2024\CESEL\Resources\CSL_SharedParameters.txt";

        [DataMember(Name = "titleBlockFamilyPath")]
        public string TitleBlockFamilyPath { get; set; } =
            @"C:\ProgramData\Autodesk\Revit\Addins\2024\CESEL\Resources\CPA-A3.rfa";

        [DataMember(Name = "lastModified")]
        public string LastModified { get; set; } = DateTime.Now.ToString("o");
    }

    public static class ConfigManager
    {
        public static AppConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                var defaultConfig = new AppConfig();
                Save(path, defaultConfig);
                return defaultConfig;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppConfig));
                    return (AppConfig)serializer.ReadObject(ms);
                }
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static void Save(string path, AppConfig config)
        {
            config.LastModified = DateTime.Now.ToString("o");
            using (var ms = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(AppConfig));
                serializer.WriteObject(ms, config);
                string json = Encoding.UTF8.GetString(ms.ToArray());

                // Pretty print manual (DataContractJsonSerializer no tiene indent nativo en .NET 4.x)
                File.WriteAllText(path, json, Encoding.UTF8);
            }
        }
    }
}