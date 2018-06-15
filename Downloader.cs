using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CurseforgeDownloader
{
    public class Downloader
    {

        //基础下载路径
        const string BASE_DOWNLOAD_URL = "https://minecraft.curseforge.com/projects/{0}/files/{1}/download";

        /// <summary>
        /// 整合包文件地址
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// .minecraft目录
        /// </summary>
        public string DirectoryPath { get; private set; }

        /// <summary>
        /// 整合包名称
        /// </summary>
        public string PackName { get; private set; }

        /// <summary>
        /// Minecraft版本
        /// </summary>
        public string MCVersion { get; private set; }

        /// <summary>
        /// 作者
        /// </summary>
        public string Author { get; private set; }

        /// <summary>
        /// 整合包版本
        /// </summary>
        public string Version { get; private set; }

        /// <summary>
        /// 要求
        /// </summary>
        public string[] Requirements { get; private set; }

        /// <summary>
        /// 最大并发数
        /// </summary>
        public int Parallelism { get; private set; }

        /// <summary>
        /// 超时尝试次数
        /// </summary>
        public int TryTime { get; private set; }

        /// <summary>
        /// 下载队列
        /// </summary>
        public ConcurrentQueue<DownloadStatus> DownloadsQueue { get; private set; }

        /// <summary>
        /// 每个下载任务的DownloadStatus
        /// </summary>
        public DownloadStatus[] Downloads { get; private set; }

        /// <summary>
        /// 下载任务的Task
        /// </summary>
        public List<Task> DownloadTasks { get; set; }

        //并发处理
        readonly LimitedConcurrencyLevelTaskScheduler _scheduler;

        DirectoryInfo _tmpDir;

        public void DeleteTempDir()
        {
            Directory.Delete(_tmpDir.FullName, true);
        }

        bool analysed = false;

        /// <summary>
        /// 创建Downloader类的实例
        /// </summary>
        /// <param name="filePath">整合包文件目录</param>
        /// <param name="directoryPath">.minecraft目录</param>
        /// <param name="parallelism">最大下载并发数</param>
        /// <param name="tryTime">超时尝试次数</param>
        public Downloader(string filePath, string directoryPath, int parallelism, int tryTime)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            DirectoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            Parallelism = parallelism;
            TryTime = tryTime;
            DownloadsQueue = new ConcurrentQueue<DownloadStatus>();
            _scheduler = new LimitedConcurrencyLevelTaskScheduler(parallelism);
        }

        public void BeginDownload()
        {
            //下载任务创建
            DownloadTasks = new List<Task>(Downloads.Length);
            TaskFactory factory = new TaskFactory(_scheduler);
            List<DownloadStatus> downloads = new List<DownloadStatus>();


            //将下载任务开始执行
            while (DownloadsQueue.TryDequeue(out DownloadStatus status))
            {
                DownloadTasks.Add(factory.StartNew(() => DownloadMod(status)));
            }
        }

        /// <summary>
        /// 解析整合包
        /// </summary>
        public bool AnalysePack()
        {
            if (analysed)
            {
                return false;
            }
            //获取临时目录
            _tmpDir = Directory.CreateDirectory(Path.GetRandomFileName());

            //解压缩
            ZipFile.ExtractToDirectory(FilePath, _tmpDir.FullName);

            //解析Json
            var json = JObject.Parse(File.ReadAllText(Path.Combine(_tmpDir.FullName, "manifest.json"), Encoding.Default));

            //解析模组
            this.PackName = (string)json["name"];
            this.Author = (string)json["author"];
            this.Version = (string)json["version"];
            this.MCVersion = (string)json["minecraft"]["version"];
            this.Requirements =
                (from m in json["minecraft"]["modLoaders"]
                 select (string)m["id"]).
                 ToArray();

            //遍历模组
            foreach (var mod in json["files"])
            {
                //将要下载的模组放入下载队列
                DownloadsQueue.Enqueue(new DownloadStatus((string)mod["projectID"], (string)mod["fileID"]));
            }
            //保存要下载的模组
            Downloads = DownloadsQueue.ToArray();

            analysed = true;
            return true;
        }

        /// <summary>
        /// 复制文件夹
        /// </summary>
        private void CopyDirectory(string source, string destination)
        {
            DirectoryInfo dir = new DirectoryInfo(source);

            foreach (var f in dir.GetFileSystemInfos())
            {
                //目标路径             
                String destName = Path.Combine(destination, f.Name);
                if (f is FileInfo)
                {
                    //如果是文件就复制       
                    File.Copy(f.FullName, destName, true);//true代表可以覆盖同名文件                     
                }
                else
                {
                    //如果是文件夹就创建文件夹然后复制然后递归复制              
                    Directory.CreateDirectory(destName);
                    CopyDirectory(f.FullName, destName);
                }
            }
        }

        /// <summary>
        /// 下载整合包
        /// </summary>
        public void DownloadPack()
        {
            //解析整合包
            AnalysePack();

            Directory.CreateDirectory(DirectoryPath);
            //复制文件夹
            CopyDirectory(Path.Combine(_tmpDir.FullName, "overrides"), DirectoryPath);

            //开始下载
            BeginDownload();

        }

        #region 下载模组
        /// <summary>
        /// 下载模组
        /// </summary>
        public void DownloadMod(DownloadStatus status)
        {
            DownloadModPrivate(status, 1);
        }

        //内部方法，用于下载失败时递归重试
        private void DownloadModPrivate(DownloadStatus status, int tryTime)
        {
            //检查下载失败
            if (tryTime > TryTime)
            {
                DownFailed?.Invoke(this, status);
                return;
            }

            string urlStr = string.Format(BASE_DOWNLOAD_URL, status.ProjectId, status.FileId);
            //检查网址正确性
            if (!Uri.TryCreate(urlStr, UriKind.Absolute, out Uri url))
                throw new FormatException("Url格式错误");

            var req = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse rep;
            //超时检查
            try
            {
                rep = (HttpWebResponse)req.GetResponse();
            }
            catch (WebException)
            {
                //超时重试
                DownloadModPrivate(status, tryTime + 1);
                return;
            }


            //文件总大小
            long totalBytes = rep.ContentLength;
            //已经下载的大小
            long downloaded = 0;
            //缓存
            byte[] buffer = new byte[1024];
            //当前时间计算反馈周期用
            DateTime startTime = DateTime.Now;
            DateTime endTime;
            //下载量数组，用于计算速度
            Queue<double> insSpeeds = new Queue<double>(20);
            for (int i = 0; i < 20; i++)
                insSpeeds.Enqueue(0.0);
            //下载速度
            int speed = 0;


            //获取文件名
            string fileName = rep.ResponseUri.Segments.Last();
            //获取文件地址
            string modPath = Path.Combine(DirectoryPath, "mods", fileName);
            //临时下载文件地址（下载完成重命名，防止下载中断导致文件损坏）
            string tmpPath = modPath + ".cfdownload";

            //保存参数
            status.TotalBytes = totalBytes;
            status.FileName = fileName;
            status.Speed = 0;
            status.DownloadedBytes = 0;


            //下载开始反馈
            status.IsBegin = true;
            DownBegin?.Invoke(this, status);


            if (File.Exists(modPath))
            {
                rep.Close();
                status.DownloadedBytes = status.TotalBytes;
                DownComplete?.Invoke(this, status);
                return;
            }

            #region 下载具体实现
            //创建文件夹
            Directory.CreateDirectory(Path.Combine(DirectoryPath, "mods"));

            //保存流
            using (FileStream fsSave = new FileStream(tmpPath,
                FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                //下载流
                using (Stream rsDown = rep.GetResponseStream())
                {
                    //单位时间内下载量
                    int periodBytes = 0;

                    int r = 1;
                    //循环读流
                    while (r > 0)
                    {

                        //计算速度
                        endTime = DateTime.Now;
                        TimeSpan span = endTime - startTime;

                        //速度计算周期50ms
                        if (span > TimeSpan.FromMilliseconds(50))
                        {
                            //瞬时速度
                            double insSpeed = periodBytes / span.TotalSeconds / 1024;

                            //重置计算
                            periodBytes = 0;
                            startTime = DateTime.Now;

                            //计算平均速度
                            insSpeeds.Dequeue();
                            insSpeeds.Enqueue(insSpeed);
                            speed = (int)insSpeeds.Average();

                        }


                        //反馈进度
                        status.DownloadedBytes = downloaded;
                        status.Speed = speed;

                        try
                        {
                            r = rsDown.Read(buffer, 0, 1024);   //读流
                            fsSave.Write(buffer, 0, r);  //存流
                        }
                        catch (IOException)
                        {
                            fsSave.Close();
                            rsDown.Close();
                            DownloadModPrivate(status, tryTime + 1);
                            return;
                        }

                        //保存进度
                        downloaded += r;
                        periodBytes += r;
                    }
                }
            }
            //下载完成，重命名文件（防止下载中断错误）
            if (File.Exists(modPath))
                File.Delete(modPath);
            File.Move(tmpPath, modPath);

            //下载完成反馈
            Thread.Sleep(100);
            DownComplete?.Invoke(this, status);
            #endregion

        }
        #endregion

        /// <summary>
        /// 在下载开始时发生
        /// </summary>
        public event DownloadEventHandler DownBegin;

        /// <summary>
        /// 在下载完成时发生
        /// </summary>
        public event DownloadEventHandler DownComplete;

        /// <summary>
        /// 在下载失败时发生
        /// </summary>
        public event DownloadEventHandler DownFailed;

    }

    public delegate void DownloadEventHandler(object sender, DownloadStatus e);
}
