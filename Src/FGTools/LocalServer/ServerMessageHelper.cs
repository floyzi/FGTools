using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace FGTools.LocalServer
{
    public static class ServerMessageHelper
    {
        public static string EncodeStr(string a)
        {
            var bytes = Encoding.UTF8.GetBytes(a);

            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
            {
                gzip.Write(bytes, 0, bytes.Length);
            }
            return Convert.ToBase64String(output.ToArray());
        }

        public static string DecodeStr(string a)
        {
            var input = new MemoryStream(Convert.FromBase64String(a));
            var gzip = new GZipStream(input, CompressionMode.Decompress);
            var reader = new StreamReader(gzip);
            return reader.ReadToEnd();
        }
    }
}
