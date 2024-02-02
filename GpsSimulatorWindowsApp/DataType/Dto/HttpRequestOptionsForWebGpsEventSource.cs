using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.DataType
{
	public class HttpRequestOptionsForWebGpsEventSource
	{
		public static HttpRequestOptionsForWebGpsEventSource DefaultHttpRequestOptions
		{
			get
			{
				return new HttpRequestOptionsForWebGpsEventSource
				{
					RequestMethod = "GET",
					RequestHeaders = new Dictionary<string, string>
					{
						{ "Accept", "application/json" },
					},
					RequestBody = string.Empty,
					GpsEventsJsonQuerySettings = WebGpsEventsJsonQuerySettings.DefaultJsonQuerySettings,
				};
			}
		}

		public HttpRequestOptionsForWebGpsEventSource()
		{
		}

		public string RequestMethod { get; set; }

		public Dictionary<string, string>? RequestHeaders { get; set; }

		public string? RequestBody { get; set; }

		public WebGpsEventsJsonQuerySettings? GpsEventsJsonQuerySettings { get; set; }
	}

	public class WebGpsEventsJsonQuerySettings
	{
		public static WebGpsEventsJsonQuerySettings DefaultJsonQuerySettings
		{
			get
			{
				return new WebGpsEventsJsonQuerySettings
				{
					JsonPathOfGpsEventList = "/Items",
					LongitudeJsonPropertyName = "Lon",
					LatitudeJsonPropertyName = "Lat",
					SpeedJsonPropertyName = "Speed",
					HeadingJsonPropertyName = "Heading",
					StartTimeJsonPropertyName = "StartTime",
				};
			}
		}

		public string JsonPathOfGpsEventList { get; set; }

		public string LongitudeJsonPropertyName { get; set; }

		public string LatitudeJsonPropertyName { get; set; }

		public string SpeedJsonPropertyName { get; set; }

		public string HeadingJsonPropertyName { get; set; }

		public string StartTimeJsonPropertyName { get; set; }
	}
}
