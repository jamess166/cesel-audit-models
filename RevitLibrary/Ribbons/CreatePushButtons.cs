#region Imported Namespaces

//.NET common used namespaces
//Revit.NET common used namespaces
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

#endregion

namespace RevitLibrary
{
    public class ButtonData
    {
        public string AvailabilityClassName { get; set; }
        public Bitmap Icon { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string AssemblyName { get; set; }
        public string ClassName { get; set; }
        public string ShortDescription { get; set; }
        public string LongDescription { get; set; }
        public string Contextual { get; set; }
    }

    public class CreatePushButtons
    {
        /// <summary>
        /// Create Bottom with tooTip
        /// </summary>
        /// <returns></returns>
        public static PushButtonData PushButton(ButtonData buttonData, Bitmap toolTip)
        {
            //Icon Image
            ImageSource imgSrc = ImageUti.GetImageSource(buttonData.Icon);

            PushButtonData btnData = PushButton(buttonData);            

            //Image of help
            ImageSource imgSrcToolTip = ImageUti.GetImageSource(toolTip);

            btnData.ToolTipImage = imgSrcToolTip;

            return btnData;
        }

        /// <summary>
        /// Create Bottom
        /// </summary>
        /// <returns></returns>
        public static PushButtonData PushButton(ButtonData buttonData)
        {
            //Icon Image
            ImageSource imgSrc = ImageUti.GetImageSource(buttonData.Icon);

            PushButtonData btnData = new PushButtonData(
                buttonData.Name,
                buttonData.Text,
                buttonData.AssemblyName,
                buttonData.ClassName
                )
            {
                ToolTip = buttonData.ShortDescription,
                LongDescription = buttonData.LongDescription,
                Image = imgSrc,
                LargeImage = imgSrc,
            };

            //Contextual Help
            ContextualHelp contextualHelp = new ContextualHelp(
                ContextualHelpType.Url, buttonData.Contextual);

            btnData.SetContextualHelp(contextualHelp);

            //Active condition Buttons
            if (buttonData.AvailabilityClassName != string.Empty)
            { btnData.AvailabilityClassName = buttonData.AvailabilityClassName; }

            return btnData;
        }
    }
}
