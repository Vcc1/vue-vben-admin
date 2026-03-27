using System.IO.Compression;
using System.Text;

namespace Infrastructure.Helper
{
    /// <summary>
    /// DataCompression 摘要说明
    /// 2024/4/19 15:00:14 add by tanhao
    /// </summary>
    public class DataCompression
    {
        /// <summary>
        /// 压缩
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string CompressString(string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            using (var memoryStream = new MemoryStream())
            {
                using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress, true))
                {
                    deflateStream.Write(buffer, 0, buffer.Length);
                    deflateStream.Close();
                }
                return Convert.ToBase64String(memoryStream.ToArray());
            }
        }

        /// <summary>
        /// 解压缩
        /// </summary>
        /// <param name="compressedText"></param>
        /// <returns></returns>
        public static string DecompressString(string compressedText)
        {
            byte[] buffer = Convert.FromBase64String(compressedText);
            using (var memoryStream = new MemoryStream(buffer))
            {
                using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress))
                {
                    using (var reader = new StreamReader(deflateStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        /// <summary>
        /// 压缩二进制数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] CompressByte(byte[] data)
        {
            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
            {
                gzipStream.Write(data, 0, data.Length);
            }
            return outputStream.ToArray();
        }

        /// <summary>
        /// 解压二进制数据
        /// </summary>
        /// <param name="compressedData"></param>
        /// <returns></returns>
        public static byte[] DecompressByte(byte[] compressedData)
        {
            using var inputStream = new MemoryStream(compressedData);
            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(outputStream);
            }
            return outputStream.ToArray();
        }
    }
}
