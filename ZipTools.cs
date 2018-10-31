using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wyUpdate
{
    public static class ZipTools
    {
        public static MemoryStream ToMemoryStream(this ZipArchiveEntry entry)
        {
            MemoryStream ms = new MemoryStream();

            using (Stream zipStream = entry.Open())
                zipStream.CopyTo(ms);

            ms.Position = 0;
            return ms;
        }
    }
}
