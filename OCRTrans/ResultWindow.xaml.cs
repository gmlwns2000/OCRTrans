using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace OCRTrans
{
    /// <summary>
    /// ResultWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ResultWindow : Window
    {
        public ResultWindow(MainWindow mw)
        {
            InitializeComponent();

            DataContext = mw;
        }

        public void SetResult(OcrResult result, OcrResult origin)
        {
            Tb_result.Text = result.GetText();
            Tb_result_original.Text = origin.GetText();
        }

        private void header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
