using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MengTuDbConfig.Client.DoMain
{
    internal static class JsonHelper
    {
        public static string GetJsonFromObj<T>(T obj)
        {
            _ = string.Empty;
            try
            {
                return JsonConvert.SerializeObject(obj);
            }
            catch
            {
                return "";
            }
        }

        public static T GetObjFromJson<T>(string strJson)
        {
            object obj = null;
            try
            {
                obj = JsonConvert.DeserializeObject<T>(strJson);
            }
            catch
            {
                return default(T);
            }

            return (T)obj;
        }
    }

}
