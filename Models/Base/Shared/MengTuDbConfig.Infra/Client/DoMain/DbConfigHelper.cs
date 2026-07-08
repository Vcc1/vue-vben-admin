//using Infrastructure.HttpApi.ChatGPT;
using MengTuDbConfig.Client;
using MengTuDbConfig.Client.Model;

namespace MengTuDbConfig.Client.DoMain
{
    internal static class DbConfigHelper
    {
        private static Dictionary<string, DbInfo> DbInfos;

        private static Timer taskTimerGetConfig;

        private static object locker;

        static DbConfigHelper()
        {
            DbInfos = new Dictionary<string, DbInfo>();
            locker = new object();
            InitGetConfigTimer();
        }

        private static void InitGetConfigTimer()
        {
            if (taskTimerGetConfig == null)
            {
                taskTimerGetConfig = new Timer(new TimerCallback(GetConfig), null, MySetting.IntervalGetConfig, 0);
            }
        }

        private static void GetConfig(object sender)
        {
            taskTimerGetConfig.Change(-1, -1);
            if (DbInfos != null && DbInfos.Count > 0)
            {
                string text = "";
                string text2 = "";
                DbEnvironment dbEnvironment = DbEnvironment.Dev;
                string text3 = "";
                string text4 = "";
                DbInfo dbInfo = null;
                bool flag = false;
                lock (locker)
                {
                    Dictionary<string, DbInfo> dictionary = new Dictionary<string, DbInfo>();
                    Dictionary<string, DbInfo> dictionary2 = new Dictionary<string, DbInfo>();
                    Dictionary<string, DbInfo> dictionary3 = new Dictionary<string, DbInfo>();
                    Result<DbInfo> result = new Result<DbInfo>();
                    string text5 = FileHelper.Read(MySetting.CacheFileInfo);
                    if (!string.IsNullOrWhiteSpace(text5))
                    {
                        dictionary3 = JsonHelper.GetObjFromJson<Dictionary<string, DbInfo>>(EncrypDecrypHelper.Decrypt(text5, MySetting.EncryKey));
                    }

                    foreach (string key in DbInfos.Keys)
                    {
                        dictionary.Add(key, DbInfos[key]);
                    }

                    string msg;
                    foreach (string key2 in dictionary.Keys)
                    {
                        text = key2.Split('#')[0];
                        text2 = key2.Split('#')[1];
                        dbEnvironment = (DbEnvironment)int.Parse(key2.Split('#')[2]);
                        text3 = key2.Split('#')[3];
                        text4 = key2.Split('#')[4];
                        dbInfo = DbInfos[key2];
                        result = GetDbInfoFromApi(text, text2, dbEnvironment, text3, text4);
                        if (result.IsSuccess)
                        {
                            string conByDBInfo = GetConByDBInfo(dbInfo, out msg);
                            string conByDBInfo2 = GetConByDBInfo(result.Data, out msg);
                            if (conByDBInfo != conByDBInfo2)
                            {
                                DbInfos[key2] = result.Data;
                                flag = true;
                            }

                            string[] array = new string[7] { text, "#", text2, "#", null, null, null };
                            int num = (int)dbEnvironment;
                            array[4] = num.ToString().Trim();
                            array[5] = "#";
                            array[6] = text3;
                            dictionary2.Add(string.Concat(array), DbInfos[key2]);
                        }
                        else if (result.Msg.Trim() == Message.IP验证失败 || result.Msg.Trim() == Message.账户验证失败 || result.Msg.Trim() == Message.账户没有对应数据库操作权限)
                        {
                            DbInfos.Remove(key2);
                            flag = true;
                        }
                        else
                        {
                            string[] array2 = new string[7] { text, "#", text2, "#", null, null, null };
                            int num2 = (int)dbEnvironment;
                            array2[4] = num2.ToString().Trim();
                            array2[5] = "#";
                            array2[6] = text3;
                            dictionary2.Add(string.Concat(array2), DbInfos[key2]);
                        }
                    }

                    foreach (KeyValuePair<string, DbInfo> item in dictionary3)
                    {
                        if (!dictionary2.ContainsKey(item.Key))
                        {
                            dictionary2.Add(item.Key, item.Value);
                            flag = true;
                        }
                        else if (GetConByDBInfo(dictionary2[item.Key], out msg) != GetConByDBInfo(item.Value, out msg))
                        {
                            flag = true;
                        }
                    }

                    if (flag || dictionary3.Count() == 0)
                    {
                        FileHelper.Clear(MySetting.CacheFileInfo);
                        FileHelper.Write(MySetting.CacheFileInfo, EncrypDecrypHelper.Encrypt(JsonHelper.GetJsonFromObj(dictionary2), MySetting.EncryKey));
                    }
                }
            }

            taskTimerGetConfig.Change(MySetting.IntervalGetConfig, 0);
        }

        private static Result<DbInfo> GetDbInfoFromApi(string url, string dbName, DbEnvironment envi, string userName, string userKey)
        {
            Result<DbInfo> result = new Result<DbInfo>
            {
                IsSuccess = true,
                Data = new DbInfo(),
                Msg = Message.正确返回
            };
            string msg = string.Empty;
            string jsons = EncrypDecrypHelper.Encrypt(JsonHelper.GetJsonFromObj(new
            {
                DbName = dbName,
                DbEnvironment = envi,
                UserName = userName
            }), MySetting.EncryKey);
            
            string toDecrypt = HttpHelper.WebRequestPost(url, jsons, userName, userKey, dbName, out msg);
            if (msg == Message.正确返回)
            {
                toDecrypt = EncrypDecrypHelper.Decrypt(toDecrypt, MySetting.EncryKey);
                result = JsonHelper.GetObjFromJson<Result<DbInfo>>(toDecrypt);
                if (result.IsSuccess)
                {
                    lock (locker)
                    {
                        string[] obj = new string[9]
                        {
                            url.Trim(),
                            "#",
                            dbName.Trim(),
                            "#",
                            null,
                            null,
                            null,
                            null,
                            null
                        };
                        int num = (int)envi;
                        obj[4] = num.ToString().Trim();
                        obj[5] = "#";
                        obj[6] = userName.Trim();
                        obj[7] = "#";
                        obj[8] = userKey.Trim();
                        string key = string.Concat(obj);
                        if (DbInfos.ContainsKey(key))
                        {
                            return result;
                        }

                        DbInfos.Add(key, result.Data);
                        return result;
                    }
                }
            }
            else
            {
                result.Msg = msg;
                result.IsSuccess = false;
                result.Data = null;
                PushMsg(url, userName, msg, dbName, envi);
            }

            return result;
        }

        private static Result<DbInfo> GetDbInfoFromFile(string url, string dbName, DbEnvironment envi, string userName)
        {
            Result<DbInfo> result = new Result<DbInfo>
            {
                IsSuccess = true,
                Data = new DbInfo(),
                Msg = Message.正确返回
            };
            try
            {
                string text = FileHelper.Read(MySetting.CacheFileInfo);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Dictionary<string, DbInfo> dictionary = new Dictionary<string, DbInfo>();
                    dictionary = JsonHelper.GetObjFromJson<Dictionary<string, DbInfo>>(EncrypDecrypHelper.Decrypt(text, MySetting.EncryKey));
                    string[] obj = new string[7]
                    {
                        url.Trim(),
                        "#",
                        dbName.Trim(),
                        "#",
                        null,
                        null,
                        null
                    };
                    int num = (int)envi;
                    obj[4] = num.ToString().Trim();
                    obj[5] = "#";
                    obj[6] = userName.Trim();
                    string key = string.Concat(obj);
                    if (dictionary.ContainsKey(key))
                    {
                        result.Data = dictionary[key];
                        return result;
                    }

                    result.IsSuccess = false;
                    result.Msg = Message.获取失败;
                    return result;
                }

                result.IsSuccess = false;
                result.Msg = Message.获取失败;
                return result;
            }
            catch (Exception ex)
            {
                result.Data = new DbInfo();
                result.IsSuccess = false;
                result.Msg = Message.未知异常;
                PushMsg(url, userName, ex.Message + ex.StackTrace, dbName, envi);
                return result;
            }
        }

        private static string GetConByDBInfo(DbInfo obj, out string msg)
        {
            string result = string.Empty;
            msg = Message.正确返回;
            if (obj != null && !string.IsNullOrEmpty(obj.ServerName) && !string.IsNullOrEmpty(obj.DbName) && !string.IsNullOrEmpty(obj.LoginName) && !string.IsNullOrEmpty(obj.LoginPwd))
            {
                result = JsonHelper.GetJsonFromObj(obj);
            }
            else
            {
                msg = Message.连接对象不完整;
            }

            return result;
        }

        public static Result<DbInfo> GetDbInfo(string url, string dbName, DbEnvironment envi, string userName, string userKey)
        {
            Result<DbInfo> result = new Result<DbInfo>
            {
                IsSuccess = true,
                Msg = Message.正确返回,
                Data = null
            };
            try
            {
                string[] obj = new string[9]
                {
                    url.Trim(),
                    "#",
                    dbName.Trim(),
                    "#",
                    null,
                    null,
                    null,
                    null,
                    null
                };
                int num = (int)envi;
                obj[4] = num.ToString().Trim();
                obj[5] = "#";
                obj[6] = userName.Trim();
                obj[7] = "#";
                obj[8] = userKey.Trim();
                string key = string.Concat(obj);
                if (DbInfos.ContainsKey(key))
                {
                    result.Data = DbInfos[key];
                }
                else
                {
                    result = GetDbInfoFromApi(url, dbName, envi, userName, userKey);
                }

                if (!result.IsSuccess)
                {
                    if (result.Msg != Message.IP验证失败)
                    {
                        if (result.Msg != Message.账户验证失败)
                        {
                            if (result.Msg != Message.账户没有对应数据库操作权限)
                            {
                                result = GetDbInfoFromFile(url, dbName, envi, userName);
                                return result;
                            }

                            return result;
                        }

                        return result;
                    }

                    return result;
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Data = null;
                result.IsSuccess = false;
                result.Msg = Message.未知异常;
                PushMsg(url, userName, ex.Message + ex.StackTrace!.ToString(), dbName, envi);
                return result;
            }
        }

        public static void PushMsg(string url, string userName, string msg, string dbName, DbEnvironment envi)
        {
            try
            {
                string msg2 = "";
                var obj = new
                {
                    UserName = userName,
                    Msg = msg,
                    DbName = dbName,
                    DbEnvironment = envi
                };
                HttpHelper.WebRequestPost(url, EncrypDecrypHelper.Encrypt(JsonHelper.GetJsonFromObj(obj), MySetting.EncryKey), "", "", dbName, out msg2);
            }
            catch (Exception ex)
            {
                FileHelper.Write(MySetting.MsgFileInfo, $"url:{url}\\r\\n userName:{userName}\\r\\n dbName:{dbName}\\r\\n envi:{envi}\\r\\n msg:{msg}\\r\\n ex:{ex.Message + ex.StackTrace}");
            }
        }
    }

}
