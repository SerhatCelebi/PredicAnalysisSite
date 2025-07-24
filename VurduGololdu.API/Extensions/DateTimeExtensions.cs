using System.Globalization;

namespace VurduGololdu.API.Extensions
{
    public static class DateTimeExtensions
    {
        private static readonly TimeZoneInfo TurkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");

        /// <summary>
        /// UTC zamanı Türkiye saatine çevirir
        /// </summary>
        public static DateTime ToTurkeyTime(this DateTime utcDateTime)
        {
            if (utcDateTime.Kind != DateTimeKind.Utc)
            {
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }
            
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, TurkeyTimeZone);
        }

        /// <summary>
        /// Türkiye saatini UTC'ye çevirir
        /// </summary>
        public static DateTime ToUtcFromTurkeyTime(this DateTime turkeyDateTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(turkeyDateTime, TurkeyTimeZone);
        }

        /// <summary>
        /// Türkiye saatinde formatlanmış string döndürür
        /// </summary>
        public static string ToTurkeyTimeString(this DateTime utcDateTime, string format = "dd.MM.yyyy HH:mm")
        {
            return utcDateTime.ToTurkeyTime().ToString(format, CultureInfo.GetCultureInfo("tr-TR"));
        }

        /// <summary>
        /// Türkiye saatinde detaylı formatlanmış string döndürür
        /// </summary>
        public static string ToTurkeyTimeDetailedString(this DateTime utcDateTime)
        {
            var turkeyTime = utcDateTime.ToTurkeyTime();
            var now = DateTime.UtcNow.ToTurkeyTime();
            var diff = now - turkeyTime;

            if (diff.TotalMinutes < 1)
                return "Şimdi";
            else if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes} dakika önce";
            else if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours} saat önce";
            else if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays} gün önce";
            else
                return turkeyTime.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("tr-TR"));
        }

        /// <summary>
        /// Şu anki Türkiye saatini döndürür
        /// </summary>
        public static DateTime NowInTurkey()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TurkeyTimeZone);
        }

        /// <summary>
        /// Bugün Türkiye saatinde başlangıç zamanı (00:00:00)
        /// </summary>
        public static DateTime TodayInTurkey()
        {
            return NowInTurkey().Date;
        }
    }
} 