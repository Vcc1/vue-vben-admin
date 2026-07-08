namespace MengTuDbConfig.Client.Model
{
    internal class MySetting
    {
        public static int IntervalGetConfig = 120000;

        public static string ConfigUrl = "http://dbconfig.m-tu.com/Api/Config/Obtain";

        public static string MsgUrl = "http://dbconfig.m-tu.com/Api/Msg/Push";

        public static string EncryKey = "mengtudbconfigif";

        public static string CacheFileInfo
        {
            get
            {
                if (AppDomain.CurrentDomain.BaseDirectory.EndsWith("\\"))
                {
                    return AppDomain.CurrentDomain.BaseDirectory + "DBINFO.txt";
                }

                return AppDomain.CurrentDomain.BaseDirectory + "\\DBINFO.txt";
            }
        }

        public static string MsgFileInfo
        {
            get
            {
                if (AppDomain.CurrentDomain.BaseDirectory.EndsWith("\\"))
                {
                    return AppDomain.CurrentDomain.BaseDirectory + "DBMSGINFO.txt";
                }

                return AppDomain.CurrentDomain.BaseDirectory + "\\DBMSGINFO.txt";
            }
        }
    }

}
