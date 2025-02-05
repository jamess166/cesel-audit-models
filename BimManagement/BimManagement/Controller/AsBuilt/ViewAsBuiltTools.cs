using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement
{
    internal static class ViewAsBuiltTools
    {
        public static string _01_11_DSI_Activo { get; set; } = "590 - Caballo Muerto 03";
        public static double _02_02_DSI_Longitud { get; set; } = 4.40;
        public static double _02_03_DSI_Espesor_solado { get; set; } = 0.10;
        public static double _02_03_DSI_Espesor_muro { get; set; } = 0.25;
        public static double _02_03_DSI_Espesor_losaFondo { get; set; } = 0.25;
        public static double _02_03_DSI_Espesor_LosaSuperior { get; set; } = 0.25;
        public static double _02_04_DSI_Altura { get; set; } = 2.00;
        public static bool IsCM3 { get; set; } = true;
        public static bool IsConduction { get; set; } = true;
    }

    internal static class ViewSetDatesTools
    {
        public static string NameSheetExcel { get; set; }
    }
}
