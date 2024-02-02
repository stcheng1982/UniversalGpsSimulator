using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.Helpers
{
	public class TimeZoneUtility
	{
		public static DateTime MinDate => new DateTime(1899, 12, 30, 12, 0, 0);

		public static TimeSpan GetTimeDaylightDelta(TimeZoneInfo clientTimeZone, DateTime dt)
		{
			var adjustmentRule = clientTimeZone.GetAdjustmentRules()
				.Where(x => x.DateStart <= dt && x.DateEnd >= dt)
				.FirstOrDefault();
			TimeSpan timeSpanDaylightDelta = TimeSpan.Zero;
			if (clientTimeZone.IsDaylightSavingTime(dt) && adjustmentRule != null)
			{
				timeSpanDaylightDelta = adjustmentRule.DaylightDelta;
			}

			return timeSpanDaylightDelta;
		}

		public static int GetUtcOffsetInMinutesByTimeZoneId(string timeZoneId, bool isRespectDaylight = true)
		{
			var clientTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
			var daylightDelta = TimeZoneUtility.GetTimeDaylightDelta(clientTimeZone, DateTime.UtcNow);
			TimeSpan timeSpan;
			if (isRespectDaylight)
			{
				timeSpan = daylightDelta + clientTimeZone.BaseUtcOffset;
			}
			else
			{
				timeSpan = clientTimeZone.BaseUtcOffset;
			}

			return Convert.ToInt32(timeSpan.TotalMinutes);
		}

		public static DateTime GetDateTimeInTimeZone(string timeZoneId, DateTime dt)
		{
			return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(dt, timeZoneId);
		}
	}
}
