using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement
{
    public class SharedParameter
    {
        public string Name { get; }
        public Guid Guid { get; }

        public SharedParameter(string name, Guid guid)
        {
            Name = name;
            Guid = guid;
        }
    }

    public static class SharedParameters
    {
        public static SharedParameter _01_01_DSI_Proyecto = new SharedParameter("01_01_DSI_Proyecto", new Guid("3de3378b-6a6c-4a1e-a218-f3ce9fb675e1"));
        public static SharedParameter _01_02_DSI_Localizador = new SharedParameter("01_02_DSI_Localizador", new Guid("b69aaf33-86d3-45aa-8f15-50cfa3dbd82e"));
        public static SharedParameter _01_03_DSI_Estado = new SharedParameter("01_03_DSI_Estado", new Guid("9e23e000-f7df-492c-b73e-d153f7e8d1f7"));
        public static SharedParameter _01_04_DSI_Clasificación = new SharedParameter("01_04_DSI_Clasificación", new Guid("77426448-09bc-49ce-a512-86b184ed4036"));
        public static SharedParameter _01_05_DSI_Tipología = new SharedParameter("01_05_DSI_Tipología", new Guid("c025784c-c545-4191-aeed-796a3994d0ec"));
        public static SharedParameter _01_06_DSI_Disciplina = new SharedParameter("01_06_DSI_Disciplina", new Guid("f11e8a5b-4c87-4501-87e0-c14ff3609094"));
        public static SharedParameter _01_07_DSI_Subdisciplina = new SharedParameter("01_07_DSI_Subdisciplina", new Guid("e0190257-9c1a-4b1d-9d6d-87cc0bef9079"));
        public static SharedParameter _01_08_DSI_Material = new SharedParameter("01_08_DSI_Material", new Guid("032631da-c161-43fc-be6b-10fce5c05219"));
        public static SharedParameter _01_09_DSI_Contratista = new SharedParameter("01_09_DSI_Contratista", new Guid("af72b721-8000-4b09-b73f-7f6f8fffdaab"));
        public static SharedParameter _01_10_DSI_Subcontratista = new SharedParameter("01_10_DSI_Subcontratista", new Guid("ffc1c683-852b-4bcc-844d-d78eb62af472"));
        public static SharedParameter _01_11_DSI_Activo = new SharedParameter("01_11_DSI_Activo", new Guid("fa51f01a-6876-4281-a1eb-dc9548b64311"));
        public static SharedParameter _02_01_DSI_Unidad = new SharedParameter("02_01_DSI_Unidad", new Guid("343dc0b3-4a21-43aa-91cc-548847fe9ebe"));
        public static SharedParameter _02_02_DSI_Longitud = new SharedParameter("02_02_DSI_Longitud", new Guid("6a32a343-d8bb-4633-bd93-d1f71bc0c36b"));
        public static SharedParameter _02_03_DSI_Espesor = new SharedParameter("02_03_DSI_Espesor", new Guid("989e3208-7efe-4401-9938-ecfa766e3361"));
        public static SharedParameter _02_04_DSI_Altura = new SharedParameter("02_04_DSI_Altura", new Guid("e8708bdc-97a3-41c0-bbf6-b1cff902d1bf"));
        public static SharedParameter _02_05_DSI_Area = new SharedParameter("02_05_DSI_Area", new Guid("1926e89f-6250-4791-b3ff-cb67eacb38ec"));
        public static SharedParameter _02_06_DSI_Volumen = new SharedParameter("02_06_DSI_Volumen", new Guid("5ad88ac0-c45c-43cf-94bf-e790a143df6d"));
        public static SharedParameter _02_07_DSI_Peso = new SharedParameter("02_07_DSI_Peso", new Guid("6880dce9-116a-4e97-b907-bc350c1d17c0"));
        public static SharedParameter _03_01_DSI_Fase_Obra = new SharedParameter("03_01_DSI_Fase Obra", new Guid("0cd13b34-27b2-4973-ac44-9a126954185b"));
        public static SharedParameter _03_03_01_DSI_Código_de_Partida = new SharedParameter("03_03_01_DSI_Código de Partida", new Guid("bf0af9d3-260a-4009-90a7-89ffbbd7eabf"));
        public static SharedParameter _03_03_01_DSI_Código_Uniclass_de_Partida = new SharedParameter("03_03_01_DSI_Código Uniclass de Partida", new Guid("773145a6-2e8e-41dd-a7f1-c257e0620387"));
        public static SharedParameter _03_03_01_DSI_Nombre_de_Partida = new SharedParameter("03_03_01_DSI_Nombre de Partida", new Guid("847f5111-6206-4ef3-9297-e9fdf8a19d69"));
        public static SharedParameter _03_03_01_DSI_Metrado_m3_ = new SharedParameter("03_03_01_DSI_Metrado (m3)", new Guid("ac68ca14-0351-44fb-a3b0-603f5ef82236"));
        public static SharedParameter _03_03_01_DSI_Fecha_Inicio_Programado = new SharedParameter("03_03_01_DSI_Fecha Inicio Programado", new Guid("70135bc3-4000-4821-9b61-9ae8dd201c6e"));
        public static SharedParameter _03_03_01_DSI_Fecha_Fin_Programado = new SharedParameter("03_03_01_DSI_Fecha Fin Programado", new Guid("a0680251-cad8-4a7b-b197-d954d7a079a6"));
        public static SharedParameter _03_03_01_DSI_Fecha_Inicio_Real = new SharedParameter("03_03_01_DSI_Fecha Inicio Real", new Guid("d5a4e9f7-c192-4025-8165-a0ef6b2fce00"));
        public static SharedParameter _03_03_01_DSI_Fecha_Fin_Real = new SharedParameter("03_03_01_DSI_Fecha Fin Real", new Guid("dc5160de-e494-4fc6-b392-113ca585c9a8"));
        public static SharedParameter _03_03_02_DSI_Código_de_Partida = new SharedParameter("03_03_02_DSI_Código de Partida", new Guid("2852a406-1e48-4f82-aa10-004fd010c70c"));
        public static SharedParameter _03_03_02_DSI_Código_Uniclass_de_Partida = new SharedParameter("03_03_02_DSI_Código Uniclass de Partida", new Guid("4583936d-14a9-4f63-a700-9f7ce9b49eb3"));
        public static SharedParameter _03_03_02_DSI_Nombre_de_Partida = new SharedParameter("03_03_02_DSI_Nombre de Partida", new Guid("1cee704f-6448-4bcf-946b-e15d7c6a2fb3"));
        public static SharedParameter _03_03_02_DSI_Metrado_m2_ = new SharedParameter("03_03_02_DSI_Metrado (m2)", new Guid("704c3cc3-4d80-45fe-bf80-2e5f06aa355b"));
        public static SharedParameter _03_03_02_DSI_Fecha_Inicio_Programado = new SharedParameter("03_03_02_DSI_Fecha Inicio Programado", new Guid("e0639bb3-0ab5-4b44-9b59-02bd86626456"));
        public static SharedParameter _03_03_02_DSI_Fecha_Fin_Programado = new SharedParameter("03_03_02_DSI_Fecha Fin Programado", new Guid("122e1cb6-9329-43b2-b3c0-3f40d6884d8f"));
        public static SharedParameter _03_03_02_DSI_Fecha_Inicio_Real = new SharedParameter("03_03_02_DSI_Fecha Inicio Real", new Guid("4a064ef9-9ec0-4815-8f01-dd40f749c609"));
        public static SharedParameter _03_03_02_DSI_Fecha_Fin_Real = new SharedParameter("03_03_02_DSI_Fecha Fin Real", new Guid("1a3f5465-0593-4045-9bd8-0ec89bf083a4"));
        public static SharedParameter _03_03_03_DSI_Código_de_Partida = new SharedParameter("03_03_03_DSI_Código de Partida", new Guid("ee52c15c-bfed-4604-9189-2a454ff7db97"));
        public static SharedParameter _03_03_03_DSI_Código_Uniclass_de_Partida = new SharedParameter("03_03_03_DSI_Código Uniclass de Partida", new Guid("92c17cce-88d8-4773-bd3e-2a5a4b2024d8"));
        public static SharedParameter _03_03_03_DSI_Nombre_de_Partida = new SharedParameter("03_03_03_DSI_Nombre de Partida", new Guid("3d635830-5545-4b56-b9ed-5662a268683b"));
        public static SharedParameter _03_03_03_DSI_Metrado_Kg_ = new SharedParameter("03_03_03_DSI_Metrado (Kg.)", new Guid("83332da1-48e1-47df-85c0-7967b12df291"));
        public static SharedParameter _03_03_03_DSI_Fecha_Inicio_Programado = new SharedParameter("03_03_03_DSI_Fecha Inicio Programado", new Guid("fe9fcf95-a5d4-4875-946e-88880e993670"));
        public static SharedParameter _03_03_03_DSI_Fecha_Fin_Programado = new SharedParameter("03_03_03_DSI_Fecha Fin Programado", new Guid("3ee7d14f-6974-48b2-9dd3-e635d70bf0d7"));
        public static SharedParameter _03_03_03_DSI_Fecha_Inicio_Real = new SharedParameter("03_03_03_DSI_Fecha Inicio Real", new Guid("94caf4a4-e4ed-4150-aa7e-638d085abf6c"));
        public static SharedParameter _03_03_03_DSI_Fecha_Fin_Real = new SharedParameter("03_03_03_DSI_Fecha Fin Real", new Guid("80af3c39-703c-46a9-9c66-268224df4d27"));
        public static SharedParameter _04_01_DSI_Controles_de_Calidad = new SharedParameter("04_01_DSI_Controles de Calidad", new Guid("f624edaa-5205-4eb6-a161-281ba1ac114c"));
        public static SharedParameter _04_02_DSI_Fotografías = new SharedParameter("04_02_DSI_Fotografías", new Guid("12950163-217f-4e10-ae5f-9743e1271e58"));
        public static SharedParameter _04_05_DSI_Certificaciones = new SharedParameter("04_03_DSI_Certificaciones", new Guid("8a0d0997-eb83-4f5b-b2e2-c755bfc2e7d7"));
        public static SharedParameter _04_04_DSI_Planos_AsBuilt = new SharedParameter("04_04_DSI_Planos AsBuilt", new Guid("e09e5c8b-461d-498f-9dee-df7d661d8d2f"));
        public static SharedParameter _03_03_04_DSI_Progresiva_Inicio = new SharedParameter("03_03_04_DSI_Progresiva Inicio", new Guid("29bb0066-9687-4d5c-ad8c-8cca12413ea1"));
        public static SharedParameter _03_03_04_DSI_Progresiva_Final = new SharedParameter("03_03_04_DSI_Progresiva Final", new Guid("dfca2ce2-1667-4c87-bff8-8912870c082b"));


        public static IList<SharedParameter> GetAllSharedParameters()
        {
            var sharedParameters = new List<SharedParameter>();
            var fields = typeof(SharedParameters).GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(SharedParameter))
                {
                    sharedParameters.Add((SharedParameter)field.GetValue(null));
                }
            }

            return sharedParameters;
        }
    }

}
