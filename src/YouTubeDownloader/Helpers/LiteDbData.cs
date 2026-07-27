using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeDownloader.Helpers
{
    public  class LiteDbData
    {
        public    Guid  Id { get; set; }= Guid.NewGuid();
        public  string Path { get; set; }
        public DateTime CreatedOn { get; set; }=DateTime.Now;
        public string CreatedBy { get; set; } = Environment.MachineName;
        public DateTime? UpdatedOn { get; set; }
        public string UpdatedBy { get; set; }
    }
}
