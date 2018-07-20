using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OCRTrans
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public bool IsDebug { get; set; } = false;

        bool followWindow = true;
        public bool FollowWindow { get => followWindow; set { followWindow = value; OnPropertyChanged(); } }
        bool isTopMost = true;
        public bool IsTopMost { get => isTopMost; set { isTopMost = value; OnPropertyChanged(); } }
        double mainOpacity = 1.0;
        public double MainOpacity { get => mainOpacity; set { mainOpacity = value; OnPropertyChanged(); } }

        ResultWindow result;
        OcrTranslater trans;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            Util.Logger.WriteMethod = new Util.Logger.WriteMethodDelegate((s) => Console.Write(s));

            InitializeComponent();

            trans = new OcrTranslater();
            trans.Translated += Trans_Translated;
            trans.From = OCRTrans.Language.English;

            DispatcherTimer timer = new DispatcherTimer();
            timer.Tick += delegate
            {
                UpdateViewport();
            };
            timer.Interval = TimeSpan.FromMilliseconds(11);
            timer.Start();

            result = new ResultWindow(this);
            result.Show();

            Loaded += delegate
            {
                UpdateViewport();
                trans.Start();
            };
        }

        void UpdateViewport()
        {
            trans.Viewport = new Util.Rect(Left + 1, Top + 1, ActualWidth - 19, ActualHeight - 2);
            if (FollowWindow)
            {
                result.Left = Left;
                result.Top = Top + ActualHeight;
                result.Width = Width;
            }
        }

        void Trans_Translated(object sender, TranslatedArg e)
        {
            result.Dispatcher.Invoke(() =>
            {
                result.SetResult(e.Results, e.Original);
            });
            if (IsDebug)
            {
                OpenCvSharp.Cv2.ImShow("result", e.Frame);
                OpenCvSharp.Cv2.WaitKey(1);
            }
        }

        void Rectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
