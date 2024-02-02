using NmeaParser.Messages;
using GpsSimulatorWindowsApp.DataType;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;
using GpsSimulatorWindowsApp.Logging;

namespace GpsSimulatorWindowsApp.Helpers
{
	public class GpsEventPlaybackDataHelper
	{
		public const string DefaultPatrolfinderHistoryEventsApiBaseUrl = "";
		public const string DefaultPatrolfinderHistoryEventsDeviceId = "9a48ce1d5dcaa4b2";
		public const string DefaultPatrolfinderHistoryEventsStartTimeValue = "2023-03-30 21:54:35";
		public const string DefaultPatrolfinderHistoryEventsEndTimevalue = "2023-03-30 23:54:35";

		public static string ExtractPFHistoryEventsBaseApiUrl(string fullApiUrl)
		{
			// Need to compose full WebGpsDataResourceUrl from patrolfinder specific parameters
			var startOfQueryString = fullApiUrl.IndexOf("?");
			var patrolfinderWebGpsEventsBaseUrl = startOfQueryString > 0 ? fullApiUrl.Substring(0, startOfQueryString) : fullApiUrl;

			return patrolfinderWebGpsEventsBaseUrl;
		}

		public static string ComposePatrolfinderHistoryEventsApiUrl(string historyEventsBaseUrl, string deviceId, string startTimeValue, string endTimeValue)
		{
			if (string.IsNullOrEmpty(historyEventsBaseUrl))
			{
				historyEventsBaseUrl = DefaultPatrolfinderHistoryEventsApiBaseUrl;
			}

			if (!historyEventsBaseUrl.EndsWith("/device"))
			{
				historyEventsBaseUrl += "/device";
			}

			//var apiUrl = $"{historyEventsBaseUrl}device?deviceId={deviceId}&startTime={startTimeValue}&endTime={endTimeValue}";
			var apiUrl = $"{historyEventsBaseUrl}?deviceId={deviceId}&startTime={startTimeValue}&endTime={endTimeValue}";

			return apiUrl;
		}

		public static async Task<List<HistoryGpsEvent>> GetHistoryGpsEventsFromWebResourceAsync(
			string webResourceUrl,
			HttpRequestOptionsForWebGpsEventSource webGpsResourceRequestOptions,
			bool usePatrolfinderHistoryEventsApi,
			string deviceId,
			DateTime startDateTime,
			DateTime endDateTime,
			bool reverseEvents)
		{
			try
			{
				// If usePatrolfinderHistoryEventsApi is checked, we check if the webResourceUrl is already a valid full url
				// If not, we compose a full pf history events api url based on parameters
				if (usePatrolfinderHistoryEventsApi)
				{
					if (string.IsNullOrEmpty(webResourceUrl))
					{
						webResourceUrl = DefaultPatrolfinderHistoryEventsApiBaseUrl;
					}

					if (webResourceUrl.IndexOf("deviceId=") <= 0)
					{
						var pfHistoryEventsApiBaseUrl = webResourceUrl;
						var startTimeValue = startDateTime.ToString("yyyy-MM-dd HH:mm:ss");
						var endTimeValue = endDateTime.ToString("yyyy-MM-dd HH:mm:ss");

						webResourceUrl = ComposePatrolfinderHistoryEventsApiUrl(
							pfHistoryEventsApiBaseUrl,
							deviceId,
							startTimeValue,
							endTimeValue
							);
					}
				}

				string apiUrl = webResourceUrl; // The general-purpose url for fetching history gps events

				var httpFactory = App.Current.Services.GetService<IHttpClientFactory>();
				var http = httpFactory.CreateClient();

				if (webGpsResourceRequestOptions?.RequestHeaders?.Any() == true)
				{
					var headersDict = webGpsResourceRequestOptions?.RequestHeaders as Dictionary<string, string>;
					foreach (var header in headersDict)
					{
						if (http.DefaultRequestHeaders.Contains(header.Key))
						{
							// Clear existing header value
							http.DefaultRequestHeaders.Remove(header.Key);
						}

						http.DefaultRequestHeaders.Add(header.Key, header.Value);
					}
				}

				var reqMethod = new HttpMethod(webGpsResourceRequestOptions.RequestMethod);
				var reqMessage = new HttpRequestMessage(reqMethod, apiUrl);
				if (reqMethod == HttpMethod.Post)
				{
					if (!string.IsNullOrEmpty(webGpsResourceRequestOptions.RequestBody))
					{
						reqMessage.Content = new StringContent(webGpsResourceRequestOptions.RequestBody, Encoding.UTF8, "application/json");
					}
				}

				var resp = await http.SendAsync(reqMessage);
				if (resp.IsSuccessStatusCode)
				{
					using (var respStream = await resp.Content.ReadAsStreamAsync())
					{
						var eventsInResponse = await ExtractHistoryGpsEventsFromDataStreamAsync(respStream, webGpsResourceRequestOptions.GpsEventsJsonQuerySettings);
						eventsInResponse.Sort((evt1, evt2) => evt1.StartTime.CompareTo(evt2.StartTime));

						if (!reverseEvents)
						{
							return eventsInResponse;
						}
						
						var eventsInReverseOrder = ReverseHistoryGpsEvents(eventsInResponse);
						return eventsInReverseOrder;
					}
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error in GetHistoryGpsEventsFromWebResourceAsync: {ex}");
			}

			return new List<HistoryGpsEvent>();
		}

		public static List<HistoryGpsEvent> ReverseHistoryGpsEvents(List<HistoryGpsEvent> historyGpsEvents)
		{
			if (historyGpsEvents?.Any() != true)
			{
				return historyGpsEvents;
			}

			// First, reverse the order of all events
			historyGpsEvents.Reverse();

			// Second, reverse the heading of all events
			foreach (var gpsEvent in historyGpsEvents)
			{
				if (gpsEvent.Heading.HasValue)
				{
					gpsEvent.Heading = gpsEvent.Heading.Value + 180;
					if (gpsEvent.Heading > 360)
					{
						gpsEvent.Heading -= 360;
					}
				}
			}

			// Third, reverse the StartTime of all events by swapping the StartTime of the event pair from begin and end until they meet in the middle
			int beginIndex = 0;
			int endIndex = historyGpsEvents.Count - 1;
			while (beginIndex < endIndex)
			{
				var beginEvent = historyGpsEvents[beginIndex];
				var endEvent = historyGpsEvents[endIndex];

				var beginStartTime = beginEvent.StartTime;
				var endStartTime = endEvent.StartTime;
				beginEvent.StartTimeValue = endStartTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
				endEvent.StartTimeValue = beginStartTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

				beginIndex++;
				endIndex--;
			}

			return historyGpsEvents;
		}

		public static decimal SafeConvertToDecimal(double value)
		{
			try
			{
				return Convert.ToDecimal(value);
			}
			catch (OverflowException)
			{
				return 0;
			}
		}

		public static async Task<List<HistoryGpsEvent>> ExtractHistoryGpsEventsFromDataStreamAsync(Stream stream, WebGpsEventsJsonQuerySettings jsonQuerySettings)
		{
			var historyGpsEvents = new List<HistoryGpsEvent>();
			using (var reader = new StreamReader(stream))
			{
				// Use System.Text.Json JsonDocument to parse the stream
				using (var jsonDoc = await JsonDocument.ParseAsync(stream))
				{
					var propertyNamesInPath = GetPropertyNamesFromJsonQueryPath(jsonQuerySettings.JsonPathOfGpsEventList);
					JsonElement elementOfEventList = GetJsonElementOfHistoryEventList(jsonDoc, propertyNamesInPath);

					DateTime autoGeneratedTime = DateTime.UtcNow;
					foreach (var eventElement in elementOfEventList.EnumerateArray())
					{
						var longitudeElement = eventElement.GetProperty(jsonQuerySettings.LongitudeJsonPropertyName);
						var latitudeElement = eventElement.GetProperty(jsonQuerySettings.LatitudeJsonPropertyName);
						var speedElement = eventElement.GetProperty(jsonQuerySettings.SpeedJsonPropertyName);
						var headingElement = eventElement.GetProperty(jsonQuerySettings.HeadingJsonPropertyName);
						var startTimeElement = eventElement.GetProperty(jsonQuerySettings.StartTimeJsonPropertyName);

						if (longitudeElement.ValueKind != JsonValueKind.Null && latitudeElement.ValueKind != JsonValueKind.Null)
						{
							decimal longitude = SafeConvertToDecimal(longitudeElement.GetDouble());
							decimal latitude = SafeConvertToDecimal(latitudeElement.GetDouble());
							decimal? speed = speedElement.ValueKind == JsonValueKind.Null ? null : speedElement.GetDecimal();
							decimal? heading = headingElement.ValueKind == JsonValueKind.Null ? null : headingElement.GetDecimal();
							DateTime? startTime = startTimeElement.ValueKind == JsonValueKind.Null ? null : DateTime.Parse(startTimeElement.GetString());

							var gpsEvent = new HistoryGpsEvent
							{
								Longitude = longitude,
								Latitude = latitude,
								Speed = speed,
								Heading = heading,
								StartTimeValue = startTime.HasValue ? startTime.Value.ToString("yyyy-MM-dd HH:mm:ss.fff") : autoGeneratedTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
							};

							historyGpsEvents.Add(gpsEvent); // add to result list
							autoGeneratedTime = autoGeneratedTime.AddMilliseconds(1000); // increment auto-generated time by 1 second
						}
					}
				}
			}

			return historyGpsEvents;
		}

		private static JsonElement GetJsonElementOfHistoryEventList(JsonDocument jsonDoc, List<string> propertyNamesInPath)
		{
			if (propertyNamesInPath?.Any() != true)
			{
				return jsonDoc.RootElement;
			}

			JsonElement currentElement = jsonDoc.RootElement;
			for (int i = 0; i < propertyNamesInPath.Count; i++)
			{
				var propertyName = propertyNamesInPath[i];
				var subElement = currentElement.GetProperty(propertyName);
				currentElement = subElement;
			}

			return currentElement;
		}

		private static List<string> GetPropertyNamesFromJsonQueryPath(string jsonQueryPath)
		{
			var propertyNames = new List<string>();
			if (jsonQueryPath.StartsWith("/"))
			{
				jsonQueryPath = jsonQueryPath.Substring(1).Trim();
			}

			var jsonPathSegments = jsonQueryPath.Split('/');
			foreach (var jsonPathSegment in jsonPathSegments)
			{
				if (!string.IsNullOrEmpty(jsonPathSegment))
				{
					propertyNames.Add(jsonPathSegment);
				}
			}

			return propertyNames;
		}	
	}
}
