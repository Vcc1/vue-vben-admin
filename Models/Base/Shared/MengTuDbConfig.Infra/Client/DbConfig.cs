
//using Infrastructure.HttpApi.ChatGPT;
using MengTuDbConfig.Client.DoMain;
using MengTuDbConfig.Client.Model;
using NLog;
using System;

namespace MengTuDbConfig.Client
{
    public static class DbConfig
    {
        public static Result<DbInfo> GetDbInfo(DbEnvironment envi, string dbName, string userName, string userKey)
        {
            Result<DbInfo> result = new Result<DbInfo>
            {
                Data = null,
                IsSuccess = true,
                Msg = Message.正确返回
            };

            try
            {
                //throw new Exception();
                if (string.IsNullOrWhiteSpace(dbName) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(userKey) || userKey.Trim().Length != 16 || userKey.Contains("#"))
                {
                    result.IsSuccess = false;
                    result.Msg = Message.请求参数错误;
                }
                else
                {
                    result = DbConfigHelper.GetDbInfo(MySetting.ConfigUrl, dbName, envi, userName, userKey);
                }

                if (!result.IsSuccess)
                {
                    if (result.Msg != Message.IP验证失败)
                    {
                        if (result.Msg != Message.账户验证失败)
                        {
                            if (result.Msg != Message.账户没有对应数据库操作权限)
                            {
                                DbConfigHelper.PushMsg(MySetting.MsgUrl, userName, "未能正常获取信息 " + result.Msg, dbName, envi);
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
                NLog.Logger Logger = NLog.LogManager.Setup().LoadConfigurationFromFile("Configs/nlog.config").GetCurrentClassLogger();
                Logger.Error(ex, "获取数据库连接字符串未知异常");
                result.IsSuccess = false;
                result.Msg = Message.未知异常;
                result.Data = null;
                DbConfigHelper.PushMsg(MySetting.MsgUrl, userName, ex.Message + ex.StackTrace!.ToString(), dbName, envi);
                return result;
            }
        }
    }
}

