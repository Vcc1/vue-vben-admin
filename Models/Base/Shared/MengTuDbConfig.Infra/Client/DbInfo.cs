using MengTuDbConfig.Client;

namespace MengTuDbConfig.Client
{
    public class DbInfo
    {
        public string ServerName { get; set; }

        public string ServerNameLAN { get; set; }

        public string Port { get; set; }

        public string DbName { get; set; }

        public string LoginName { get; set; }

        public string LoginPwd { get; set; }

        public Dictionary<string, string> ConProperty { get; set; }

        public DbEnvironment Envi { get; set; }
    }
}
