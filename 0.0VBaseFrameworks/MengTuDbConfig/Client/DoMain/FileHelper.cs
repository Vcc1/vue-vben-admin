using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MengTuDbConfig.Client.DoMain
{
    internal static class FileHelper
    {
        public static string Read(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    StreamReader streamReader = new StreamReader(path, Encoding.Default);
                    string result = streamReader.ReadToEnd();
                    streamReader.Close();
                    return result;
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        public static void Write(string path, string content)
        {
            try
            {
                FileStream fileStream = new FileStream(path, FileMode.OpenOrCreate);
                byte[] bytes = Encoding.Default.GetBytes(content);
                fileStream.Write(bytes, 0, bytes.Length);
                fileStream.Flush();
                fileStream.Close();
            }
            catch
            {
            }
        }

        public static void Clear(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    using FileStream fileStream = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write);
                    fileStream.Seek(0L, SeekOrigin.Begin);
                    fileStream.SetLength(0L);
                    fileStream.Close();
                }
            }
            catch
            {
            }
        }
    }

}
