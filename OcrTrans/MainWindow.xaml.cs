using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace OcrTrans
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            OcrEngine ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            var c = Capture();
            var result = ocrEngine.RecognizeAsync(c).AsTask().Result;
            var rlist = result.Lines.ToArray();
        }

        private SoftwareBitmap Capture()
        {
            using (var bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb))
            {
                using (var gfxScreenshot = Graphics.FromImage(bmpScreenshot))
                {
                    gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);

                    using (MemoryStream m = new MemoryStream())
                    {
                        bmpScreenshot.Save(m, ImageFormat.Png);
                        var decoder = Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(m.AsRandomAccessStream()).AsTask().Result;
                        var bitmap = decoder.GetSoftwareBitmapAsync().AsTask().Result;
                        //var software = SoftwareBitmap.CreateCopyFromBuffer(m.ToArray().AsBuffer(), BitmapPixelFormat.Rgba8, bmpScreenshot.Width, bmpScreenshot.Height);
                        return bitmap;
                    }
                }
            }
        }
    }
}
