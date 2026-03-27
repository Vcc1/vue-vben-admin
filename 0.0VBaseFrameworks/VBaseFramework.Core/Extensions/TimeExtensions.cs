using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    public static class TimestampExtension
    {
        // 秒级时间戳转 DateTime (int/long)
        public static DateTime ToDateTimeFromSeconds(this int seconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().DateTime;
        }

        public static DateTime ToDateTimeFromSeconds(this long seconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().DateTime;
        }

        // 毫秒级时间戳转 DateTime (int/long)
        public static DateTime ToDateTimeFromMilliseconds(this int milliseconds)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToLocalTime().DateTime;
        }

        public static DateTime ToDateTimeFromMilliseconds(this long milliseconds)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToLocalTime().DateTime;
        }

        // DateTime 转 秒级时间戳
        public static long ToUnixTimeSeconds(this DateTime dateTime)
        {
            return new DateTimeOffset(dateTime.ToUniversalTime()).ToUnixTimeSeconds();
        }

        // DateTime 转 毫秒级时间戳
        public static long ToUnixTimeMilliseconds(this DateTime dateTime)
        {
            return new DateTimeOffset(dateTime.ToUniversalTime()).ToUnixTimeMilliseconds();
        }
    }
}
