using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VBaseFramework.Core.String
{
    public class StringCompression
    {


        /// <summary>
        /// 压缩
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] CompressBrotli(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            using var ms = new MemoryStream();
            using (var brotli = new BrotliStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                brotli.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }
        /// <summary>
        /// 解压缩
        /// </summary>
        /// <param name="compressed"></param>
        /// <returns></returns>
        public static byte[] DecompressBrotli(byte[] compressed)
        {
            if (compressed == null || compressed.Length == 0)
                return Array.Empty<byte>();

            using var src = new MemoryStream(compressed);
            using var brotli = new BrotliStream(src, CompressionMode.Decompress);
            using var dst = new MemoryStream();
            brotli.CopyTo(dst);
            return dst.ToArray();
        }

        /// <summary>
        /// 尝试解压缩 Brotli 数据，如果失败则返回原始数据（兼容未压缩的旧数据）
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] TryDecompressBrotli(byte[] data)
        {
            if (data == null || data.Length == 0)
                return data;

            try
            {
                using var src = new MemoryStream(data);
                using var brotli = new BrotliStream(src, CompressionMode.Decompress);
                using var dst = new MemoryStream();
                brotli.CopyTo(dst);
                return dst.ToArray();
            }
            catch
            {
                // 如果解压失败，说明数据未压缩，直接返回原数据
                return data;
            }
        }



        /// <summary>
        /// Compresses the specified text using the Brotli compression algorithm and returns the compressed data as a
        /// byte array.
        /// </summary>
        /// <remarks>The input text is encoded using UTF-8 before compression. Brotli compression is
        /// suitable for reducing the size of textual data for storage or transmission.</remarks>
        /// <param name="text">The text to compress. If <paramref name="text"/> is null or empty, an empty byte array is returned.</param>
        /// <returns>A byte array containing the Brotli-compressed representation of the input text. Returns an empty array if
        /// the input is null or empty.</returns>
        public static byte[] CompressToBrotli(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<byte>();
            var input = Encoding.UTF8.GetBytes(text);
            using var ms = new MemoryStream();
            using (var brotli = new BrotliStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                brotli.Write(input, 0, input.Length);
            }
            return ms.ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string DecompressFromBrotli(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;
            using var src = new MemoryStream(data);
            using var brotli = new BrotliStream(src, CompressionMode.Decompress);
            using var dst = new MemoryStream();
            brotli.CopyTo(dst);
            return Encoding.UTF8.GetString(dst.ToArray());
        }



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
                using (var deflateStream = new DeflateStream(memoryStream, CompressionLevel.Fastest, true))
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
        public static string DecompressBuffer(byte[] buffer)
        {

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
    }

}