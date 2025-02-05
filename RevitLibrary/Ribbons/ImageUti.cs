using System;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Windows.Media;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace RevitLibrary
{
    public class ImageUti
    {
        public static BitmapSource GetImageSource(Image img)
        {
            BitmapImage bmp = new BitmapImage();

            using (MemoryStream ms = new MemoryStream())
            {
                img.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                bmp.BeginInit();

                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = null;
                bmp.StreamSource = ms;

                bmp.EndInit();

            }

            return bmp;
        }
    }
}
