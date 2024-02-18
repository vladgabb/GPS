using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace MyGIS
{
    public class ImageTools
    {
        public static Image ResizeImage(byte[] byteArray, int newWidth, int newHeight)
        {
            using (MemoryStream stream = new MemoryStream(byteArray))
            {
                Image originalImage = Image.FromStream(stream);
                Image resizedImage = new Bitmap(newWidth, newHeight);

                using (Graphics g = Graphics.FromImage(resizedImage))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(originalImage, 0, 0, newWidth, newHeight);
                }

                return resizedImage;
            }
        }

        public static ImageSource ByteArrayToImageSource(byte[] byteArray)
        {
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = new MemoryStream(byteArray);
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();

            // Убедитесь, что вызвано данное свойство, чтобы избежать проблем с потоком памяти
            bitmapImage.Freeze();

            return bitmapImage;
        }
    }
}
