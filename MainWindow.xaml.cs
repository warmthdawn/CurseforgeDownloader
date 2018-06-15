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
using System.Windows.Threading;

namespace CurseforgeDownloader
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("装备开始");


            string filePath;
            string dir;

            System.Windows.Forms.OpenFileDialog openFile = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "整合包文件|*.zip",
                Multiselect = false,
                Title = "请选择整合包文件",
                CheckFileExists = true
            };
            if (openFile.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                Application.Current.Shutdown();
            filePath = openFile.FileName;
            System.Windows.Forms.FolderBrowserDialog fbDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "请选择.minecraft文件夹"
            };
            if (fbDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filePath), ".minecraft");
            }
            dir = fbDialog.SelectedPath;


            //在这里修改线程数
            Downloader down = new Downloader(filePath, dir, 15/*线程数*/, 5/*下载尝试次数*/);

            down.AnalysePack();

            var downs = new Dictionary<DownloadStatus, DownloadProgress>();


            for (int i = 0; i < down.Downloads.Length; i++)
            {
                var item = down.Downloads[i];
                DownloadProgress elePro = new DownloadProgress();
                elePro.Initialize();
                downs.Add(item, elePro);
                warpContainer.Children.Add(elePro);
            }
            int completed = 0;
            int totalCount = down.Downloads.Length;
            down.DownComplete += (s, d) =>
                Dispatcher.Invoke(() =>
                {
                    downs[d].Complete();
                    completed++;
                });
            down.DownFailed += (s, d) =>
                    Dispatcher.Invoke(() =>
                        downs[d].Error(d));

            new TaskFactory().StartNew(() =>
            {
                down.DownloadPack();
                Dispatcher.Invoke(() =>
                {
                    //显示整合包参数
                    txtAuthor.Text = down.Author;
                    txtMCVersion.Text = down.MCVersion;
                    txtName.Text = down.PackName;
                    txtVersion.Text = down.Version;
                    txtReq.Text = string.Join(",", down.Requirements);
                    proTotal.Maximum = totalCount;


                    DispatcherTimer timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(300)
                    };
                    timer.Tick += (s, ev) =>
                    {
                        txtCount.Text = completed + "/" + totalCount;
                        proTotal.Value = completed;

                        foreach (var item in downs)
                        {
                            item.Value.Update(item.Key);
                        }
                        if (down.DownloadTasks.All(t => t?.IsCompleted == true))
                        {
                            timer.Stop();
                            down.DeleteTempDir();
                            MessageBox.Show("下载完成!");
                        }
                    };
                    timer.Start();
                });

            });



        }
    }
}
