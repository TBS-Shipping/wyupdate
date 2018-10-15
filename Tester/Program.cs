using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wyUpdate.Downloader;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {            
            FileDownloader fd = new FileDownloader(new List<string> { "ftp://m-uat.gnav.com:8021/wyserver.wys" }, @"C:\AlienTemp");
            fd.Download();
            Console.ReadKey();
        }
    }
}
