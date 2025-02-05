using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement
{
    public class ViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModel"/> class.
        /// </summary>
        public ViewModel()
        {
            SheetDetails = RevitTools.GetSheets();
        }

        private List<SheetDetail> _SheetDetails;

        /// <summary>
        /// Gets or sets the orders details.
        /// </summary>
        /// <value>The orders details.</value>
        public List<SheetDetail> SheetDetails
        {
            get { return _SheetDetails; }
            set { _SheetDetails = value; }
        }
    }
}
