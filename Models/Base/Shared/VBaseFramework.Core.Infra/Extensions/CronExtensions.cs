
using System;
using Cronos;

namespace Cronos
{
    public static class CronExtensions
    {
        private readonly static TimeZoneInfo LocalTimeZoneInfo = TimeZoneInfo.Local;
        public static int GetDelayMilliseconds(this CronExpression cronExpression, TimeZoneInfo timeZone)
        {
            var nowUtc = DateTime.UtcNow;
            var next = cronExpression.GetNextOccurrence(nowUtc, timeZone);

            if (!next.HasValue)
                return 0;

            return Math.Max(0, (int)(next.Value - nowUtc).TotalMilliseconds);
        }

        public static int GetDelayMilliseconds(this CronExpression cronExpression)
        {
            var nowUtc = DateTime.UtcNow;
            var next = cronExpression.GetNextOccurrence(nowUtc, LocalTimeZoneInfo);

            if (!next.HasValue)
                return -1;

            return Math.Max(0, (int)(next.Value - nowUtc).TotalMilliseconds);
        }
    }
}


