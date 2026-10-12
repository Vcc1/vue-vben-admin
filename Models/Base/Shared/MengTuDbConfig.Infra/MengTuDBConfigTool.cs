using DbTool;
using MengTuDbConfig.Client;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace DbTool
{
    public class MengTuDBConfigTool
    {

        public static void InitDBConfig(ConfigurationManager configuration, string dbname)
        {
            MengTuDbConfig = MengTuDbConfig ?? new MengTuDBConfig()
            {
                Sup_DbEnviType = configuration.GetValue<String>("MengTuDBConfig:Sup_DbEnviType"),
                Sup_DbIsUseLAN = configuration.GetValue<String>("MengTuDBConfig:Sup_DbIsUseLAN"),
                Sup_DbName = configuration.GetValue<String>("MengTuDBConfig:Sup_DbName"),
                Sup_DbPassWord = configuration.GetValue<String>("MengTuDBConfig:Sup_DbPassWord"),
                Sup_DbUserCode = configuration.GetValue<String>("MengTuDBConfig:Sup_DbUserCode"),
            };

            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(dbname)))
            {
                while (true)
                {
                    try
                    {
                        var fallback = new Dictionary<string, string>
                        {
                            [$"ConnectionStrings:{dbname}"] = MengTuDBConfigTool.GetConnectionString(dbname)
                        };

                        Console.WriteLine($"ConnectionStrings:{dbname} = {fallback[$"ConnectionStrings:{dbname}"]}");
                        configuration.AddInMemoryCollection(fallback);
                        break;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"ConnectionStrings:{dbname} 获取失败 重试 ");
                        Task.Delay(500).Wait();
                    }
                }
            }
            else
            {
                Console.WriteLine($"ConnectionStrings:{dbname} = {configuration.GetConnectionString(dbname)}");
            }
        }

        public static MengTuDBConfig MengTuDbConfig { get; set; }
        public static string GetConnectionString(string dbname)
        {

            Result<DbInfo> result = null;
            switch (MengTuDbConfig.Sup_DbEnviType)
            {
                case "1":
                    result = DbConfig.GetDbInfo(DbEnvironment.Dev, dbname, MengTuDbConfig.Sup_DbUserCode, MengTuDbConfig.Sup_DbPassWord);
                    break;
                case "2":
                    result = DbConfig.GetDbInfo(DbEnvironment.Test, dbname, MengTuDbConfig.Sup_DbUserCode, MengTuDbConfig.Sup_DbPassWord);
                    break;
                case "3":
                    result = DbConfig.GetDbInfo(DbEnvironment.Prod, dbname, MengTuDbConfig.Sup_DbUserCode, MengTuDbConfig.Sup_DbPassWord);
                    break;
            }
            if (MengTuDbConfig.Sup_DbIsUseLAN == "Y")
            {
                return "Server=" + result.Data.ServerNameLAN + "," + result.Data.Port + ";Database=" + result.Data.DbName + ";UID=" + result.Data.LoginName + ";PWD=" + result.Data.LoginPwd + ";connection timeout=6000;Encrypt=False;TrustServerCertificate=True;";
            }
            return "Server=" + result.Data.ServerName + "," + result.Data.Port + ";Database=" + result.Data.DbName + ";UID=" + result.Data.LoginName + ";PWD=" + result.Data.LoginPwd + ";connection timeout=6000;Encrypt=False;TrustServerCertificate=True;";
        }

        public static string GetConnectionString(DbEnvironment dbEnvironment, string dbname)
        {

            Result<DbInfo> result = null;
            result = DbConfig.GetDbInfo(dbEnvironment, dbname, MengTuDbConfig.Sup_DbUserCode, MengTuDbConfig.Sup_DbPassWord);
            if (MengTuDbConfig.Sup_DbIsUseLAN == "Y")
            {
                return "Server=" + result.Data.ServerNameLAN + "," + result.Data.Port + ";Database=" + result.Data.DbName + ";UID=" + result.Data.LoginName + ";PWD=" + result.Data.LoginPwd + ";connection timeout=6000;Encrypt=False;TrustServerCertificate=True;";
            }
            return "Server=" + result.Data.ServerName + "," + result.Data.Port + ";Database=" + result.Data.DbName + ";UID=" + result.Data.LoginName + ";PWD=" + result.Data.LoginPwd + ";connection timeout=6000;Encrypt=False;TrustServerCertificate=True;";
        }
    }
}
