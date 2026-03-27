//using Infrastructure.HttpApi.ChatGPT;
using MengTuDbConfig.Client.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MengTuDbConfig.Client.DoMain
{
    internal class HttpHelper
    {
        public static string WebRequestPost(string url, string jsons, string userName, string userKey, string dbName, out string msg)
        {
            string result = string.Empty;
            msg = Message.正确返回;
            byte[] bytes = Encoding.UTF8.GetBytes(jsons);
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(new Uri(url));
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.KeepAlive = false;
            httpWebRequest.ProtocolVersion = HttpVersion.Version10;
            httpWebRequest.Timeout = 5000;
            httpWebRequest.ContentLength = bytes.Length;
            DateTime utcNow = DateTime.UtcNow;
            string text = utcNow.GetDateTimeFormats('r')[0].ToString();
            httpWebRequest.Headers.Add("Authorization", dbName + " " + userName + ":" + EncrypDecrypHelper.MD5Encrypt(userKey.Trim() + dbName.Trim() + text.Trim()));
            httpWebRequest.Date = utcNow;
            Stream stream = null;
            HttpWebResponse httpWebResponse = null;
            StreamReader streamReader = null;
            try
            {
                stream = httpWebRequest.GetRequestStream();
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
                stream.Close();
                httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                stream = httpWebResponse.GetResponseStream();
                streamReader = new StreamReader(stream);
                result = streamReader.ReadToEnd();
                return result;
            }
            catch (WebException ex)
            {
                string errorResponseBody = string.Empty;
                if (ex.Response != null)
                {
                    using (HttpWebResponse errorResponse = (HttpWebResponse)ex.Response)
                    using (Stream errorStream = errorResponse.GetResponseStream())
                    {
                        if (errorStream != null)
                        {
                            using (StreamReader errorStreamReader = new StreamReader(errorStream))
                            {
                                errorResponseBody = errorStreamReader.ReadToEnd();
                            }
                        }
                    }
                }

                result = errorResponseBody;
                msg = Message.API响应错误 + ex.Message + Environment.NewLine + errorResponseBody + Environment.NewLine + ex.StackTrace!.ToString();
                return result;
            }
            finally
            {
                streamReader?.Close();
                stream?.Close();
                httpWebResponse?.Close();
            }
        }
    }

}
