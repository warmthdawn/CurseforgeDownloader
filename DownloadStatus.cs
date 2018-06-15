using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CurseforgeDownloader
{
    /// <summary>
    /// 提供下载文件的参数及下载进度，下载速度
    /// </summary>
    public class DownloadStatus
    {
        public DownloadStatus(string projectId, string fileId)
        {
            ProjectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
            FileId = fileId ?? throw new ArgumentNullException(nameof(fileId));
        }

        public DownloadStatus(string projectId, string fileId, string fileName, long totalBytes, long downloadedBytes, int speed)
        {
            ProjectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
            FileId = fileId ?? throw new ArgumentNullException(nameof(fileId));
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            TotalBytes = totalBytes;
            DownloadedBytes = downloadedBytes;
            Speed = speed;
            IsBegin = false;
        }

        #region 属性
        /// <summary>
        /// 项目ID
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// 文件ID
        /// </summary>
        public string FileId { get; set; }

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 文件总大小
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// 已下载文件大小
        /// </summary>
        public long DownloadedBytes { get; set; }

        /// <summary>
        /// 下载速度
        /// </summary>
        public int Speed { get; set; }

        /// <summary>
        /// 是否已经下载
        /// </summary>
        public bool IsBegin { get; set; }
        #endregion

    }
}
