using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitLibrary
{
    public class InterfaceRibbon
    {        
        /// <summary>
        /// Crear el Ribbon Tab y el Ribbon Panel
        /// </summary>
        /// <param name="application"></param>
        /// <param name="ribbonTab"></param>
        /// <param name="ribbonPanel"></param>
        /// <returns></returns>
        public static RibbonPanel CreateRibbonPanel(UIControlledApplication application,
            string ribbonTab, string ribbonPanel)
        {
            try { application.CreateRibbonTab(ribbonTab); }            
            catch { }

            foreach (RibbonPanel pnl in application.GetRibbonPanels(ribbonTab))
            {
                if (pnl.Name == ribbonPanel) { return pnl; }                
            }

            return application.CreateRibbonPanel(ribbonTab, ribbonPanel);
        }
    }
}
