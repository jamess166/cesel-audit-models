using System.Collections.Generic;
using System.IO;
using System.Windows.Documents;

namespace BimManagement
{
    public partial class WeeklyReportTools
    {
        public static string IssueName { get; set; }
        public static string IssueMonth { get; set; }
        public static string IssueDate { get; set; }
        public static List<FileInfo> Files { get; set; }
    }
}