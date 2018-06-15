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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CurseforgeDownloader
{
    /// <summary>
    /// DownloadProgress.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadProgress : UserControl
    {
        public DownloadProgress()
        {
            InitializeComponent();

        }

        public void Initialize()
        {
            txtSize.Text = "计算中";
            txtSpeed.Text = "计算中";
            txtName.Text = "加载中";
            colPro.Width = new GridLength(0, GridUnitType.Star);
            colProCon.Width = new GridLength(1, GridUnitType.Star);
            txtPro.Text = "%0";
        }

        public void Begin(DownloadStatus status)
        {
            txtName.Text = status.FileName;
        }
        public static string CalcSize(long size)
        {
            if (size < 1024)
                return "<1K";
            else if (size < 1024 * 1024)
                return Math.Round(size / 1024D, 2) + "K";
            else if (size < 1024L * 1024 * 1024)
                return Math.Round(size / (1024D * 1024), 2) + "M";
            else if (size < 1024L * 1024 * 1024 * 1024)
                return Math.Round(size / (1024D * 1024 * 1024 * 1024), 2) + "G";
            else return Math.Round(size / (1024D * 1024 * 1024), 2) + "T";
        }
        public void Complete()
        {
            if (error)
                return;
            border.Background = Brushes.Orange;
        }

        bool error = false;
        public void Error(DownloadStatus status)
        {
            border.Background = Brushes.Red;

            txtName.Text = "下载失败";
            colPro.Width = new GridLength(1, GridUnitType.Star);
            colProCon.Width = new GridLength(0, GridUnitType.Star);
            error = true;
        }

        public void Update(DownloadStatus status)
        {
            if (!status.IsBegin)
                return;
            if (error)
                return;
            //计算进度
            txtSize.Text = CalcSize(status.DownloadedBytes) + "/" + CalcSize(status.TotalBytes);
            double value = Math.Round((double)status.DownloadedBytes / status.TotalBytes, 3);

            //更新进度条
            colPro.Width = new GridLength(value, GridUnitType.Star);
            colProCon.Width = new GridLength(1 - value, GridUnitType.Star);

            //更新进度
            txtPro.Text = value * 100 + "%";

            //更新速度
            txtSpeed.Text = CalcSize(1024L * status.Speed) + "B/s";

            txtName.Text = status.FileName;


        }


    }
}
