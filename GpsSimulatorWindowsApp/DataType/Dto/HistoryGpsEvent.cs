using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.DataType
{
	public class HistoryGpsEvent
	{
		[JsonPropertyName("Lon")]
		public decimal? Longitude { get; set; }

		[JsonPropertyName("Lat")]
		public decimal? Latitude { get; set; }

		[JsonPropertyName("Speed")]
		public decimal? Speed { get; set; }

		[JsonPropertyName("Heading")]
		public decimal? Heading { get; set; }

		[JsonPropertyName("StartTime")]
		public string StartTimeValue { get; set; }

		[JsonIgnore]
		public DateTime StartTime
		{
			get
			{
				DateTime output;
				if (DateTime.TryParse(StartTimeValue, out output))
				{
					return DateTime.SpecifyKind(output, DateTimeKind.Utc);
				}

				if (DateTime.TryParseExact(StartTimeValue, "mm:ss.f", CultureInfo.InvariantCulture, DateTimeStyles.None, out output))
				{
					return DateTime.SpecifyKind(output, DateTimeKind.Utc);
				}

				return DateTime.MinValue;
			}
		}
	}
}
