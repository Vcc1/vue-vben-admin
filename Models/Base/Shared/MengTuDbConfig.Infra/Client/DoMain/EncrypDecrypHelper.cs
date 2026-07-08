using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MengTuDbConfig.Client.DoMain
{
    internal static class EncrypDecrypHelper
    {
        public static string Encrypt(string toEncrypt, string key)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(key);
            byte[] bytes2 = Encoding.UTF8.GetBytes(toEncrypt);
            byte[] array = new RijndaelManaged
            {
                Key = bytes,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            }.CreateEncryptor().TransformFinalBlock(bytes2, 0, bytes2.Length);
            return Convert.ToBase64String(array, 0, array.Length);
        }

        public static string Decrypt(string toDecrypt, string key)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(key);
            byte[] array = Convert.FromBase64String(toDecrypt);
            byte[] bytes2 = new RijndaelManaged
            {
                Key = bytes,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            }.CreateDecryptor().TransformFinalBlock(array, 0, array.Length);
            return Encoding.UTF8.GetString(bytes2);
        }

        public static string MD5Encrypt(string toEncrypt)
        {
            MD5CryptoServiceProvider mD5CryptoServiceProvider = new MD5CryptoServiceProvider();
            byte[] bytes = Encoding.UTF8.GetBytes(toEncrypt);
            bytes = mD5CryptoServiceProvider.ComputeHash(bytes);
            mD5CryptoServiceProvider.Clear();
            string text = "";
            for (int i = 0; i < bytes.Length; i++)
            {
                text += Convert.ToString(bytes[i], 16).PadLeft(2, '0');
            }

            return text.PadLeft(32, '0');
        }
    }

}
