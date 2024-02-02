using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Logging;
using NmeaParser.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.Helpers
{
	public class NmeaDataHelper
	{
		public const decimal KnotsToKmphFactor = 1.85m;
		public const string RmcMessageType = "GPRMC";
		public const string RmcSentencePrefix = "$GPRMC";
		public const string NmeaSentencesBeforeRmcSentence =
@"$GPGGA,012227.0,4243.099496,N,07346.133224,W,1,04,1.3,112.7,M,-35.0,M,,*6B
$GNGNS,012227.0,4243.099496,N,07346.133224,W,AAN,06,1.3,112.7,-35.0,,*13
$GPVTG,224.3,T,235.5,M,14.3,N,26.6,K,A*21
";
		public const string NmeaSentencesAfterRmcSentences =
@"$GPGSA,A,2,02,11,12,25,29,,,,,,,,1.6,1.3,1.0*3A
$GNGSA,A,2,02,11,12,25,29,,,,,,,,1.6,1.3,1.0,1*39
$GNGSA,A,2,81,67,,,,,,,,,,,1.6,1.3,1.0,2*3F
$GNGSA,A,2,,,,,,,,,,,,,1.6,1.3,1.0*29
$GNGSA,A,2,,,,,,,,,,,,,1.6,1.3,1.0,3*36
$GLGSV,3,1,10,66,57.7,45.0,26.1,82,59.8,340.3,25.6,73,6.3,312.2,22.6,88,6.3,122.3,23.4*5B
$GLGSV,3,2,10,83,18.3,315.0,23.3,81,55.5,99.8,38.4,67,59.8,184.2,39.2,68,10.5,201.1,28.6*5C
$GLGSV,3,3,10,74,2.8,353.0,,65,4.9,29.5,*58
$GPGSV,4,1,13,02,31.6,298.1,34.1,05,68.9,254.5,23.0,06,19.0,97.0,24.6,09,12.0,38.0,25.4*7B
$GPGSV,4,2,13,11,57.7,75.9,31.6,12,13.4,222.2,31.4,13,22.5,163.1,32.4,25,17.6,253.1,26.3*46
$GPGSV,4,3,13,29,42.9,306.6,39.1,48,,,32.7,07,0.7,66.1,,15,8.4,194.1,*7E
$GPGSV,4,4,13,20,74.5,29.5,*71
";

		public const string NmeaRmcSentenceWithNoGeolocation = "$GPRMC,,V,,,,,,,,,,N*53";
		public const string NullDeviceProfileName = "NULL";
		public const string CradleDeviceProfileName = "Cradlepoint";

		private static readonly HashSet<string> _deviceProfileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { NullDeviceProfileName, CradleDeviceProfileName };

		public static string[] DeviceProfileNames
		{
			get
			{
				return _deviceProfileNames.ToArray();
			}
		}

		public static string DefaultSampleNmeaFilePath
		{
			get
			{
				return Path.Combine(AppContext.BaseDirectory, @".\Data\NMEA\Panasonic_GPS_V_Log.nmea"); // Default one
			}
		}

		public static string GetRmcSentenceWithInvalidGeolocation()
		{
			return NmeaRmcSentenceWithNoGeolocation;
		}

		public static async Task<string?> TryValidatingNmeaDataFileAsync(string nmeaDataFilePath)
		{
			try
			{
				var events = await GetHistoryGpsEventsFromNmeaFileAsync(nmeaDataFilePath);
				if (events?.Any() == true)
				{
					return null;
				}

				return $"No Gps Events detected in file '{nmeaDataFilePath}'";
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}

		public static async Task<List<HistoryGpsEvent>> GetHistoryGpsEventsFromNmeaFileAsync(string nmeaDataFilePath)
		{
			if (string.IsNullOrEmpty(nmeaDataFilePath) || !File.Exists(nmeaDataFilePath))
			{
				throw new ArgumentNullException(nameof(nmeaDataFilePath));
			}

			using var fs = OpenAsyncReadOnlyFileStream(nmeaDataFilePath);
			using (var sr = new StreamReader(fs))
			{
				var events = new List<HistoryGpsEvent>();
				while (!sr.EndOfStream)
				{
					var line = await sr.ReadLineAsync();
					if (!string.IsNullOrEmpty(line) && line.StartsWith(RmcSentencePrefix))
					{
						if (TryParseNmeaRmcSentenceAsHistoryGpsEvent(line, out HistoryGpsEvent gpsEvent))
						{
							events.Add(gpsEvent);
						}
					}
				}

				return events;
			}
		}

		public static async Task<(bool exported, string error)> SaveHistoryGpsEventsAsNmeaFileAsync(List<HistoryGpsEvent> historyGpsEvents, string nmeaDataFilePath)
		{
			if (historyGpsEvents?.Any() != true)
			{
				return (false, "Invalid History Events");
			}

			if (string.IsNullOrEmpty(nmeaDataFilePath))
			{
				return (false, "Invalid Nmea File Path");
			}

			try
			{
				using (var sw = new StreamWriter(nmeaDataFilePath))
				{
					await sw.WriteAsync(NmeaSentencesAfterRmcSentences);
					foreach (var gpsEvent in historyGpsEvents)
					{
						await sw.WriteAsync(NmeaSentencesBeforeRmcSentence);
						var rmcSentence = ConvertHistoryGpsEventToNmeaRmcSentence(gpsEvent, gpsEvent.StartTime, gpsEvent.Speed);
						await sw.WriteAsync(rmcSentence);
						await sw.WriteAsync(NmeaSentencesAfterRmcSentences);
					}
				}

				return (true, nmeaDataFilePath);
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error in SaveHistoryGpsEventsAsNmeaFileAsync: {ex}");
				return (false, ex.Message);
			}
		}

		public static string BuildGprmcSentence(decimal? longitude, decimal? latitude, decimal? speed, decimal? heading, DateTime date)
		{
			// Convert speed from km/h to knots
			decimal? speedKnots = speed.HasValue ? speed.Value / KnotsToKmphFactor : null;

			string formattedLatitude = FormatLatitude(latitude);
			string formattedLongitude = FormatLongitude(longitude);
			string formattedSpeed = speedKnots.HasValue ? speedKnots.Value.ToString("0.000") : string.Empty;
			string formattedHeading = heading.HasValue ? heading.Value.ToString("0.00") : string.Empty;
			string formattedDate = date.ToString("ddMMyy");
			string formattedTime = date.ToString("HHmmss.00");

			string sentenceBody = string.Format(
				"GPRMC,{0},A,{1},{2},{3},{4},{5},,,A",
				formattedTime,
				formattedLatitude,
				formattedLongitude,
				formattedSpeed,
				formattedHeading,
				formattedDate);

			string checkSum = GetChecksum(sentenceBody);

			return $"${sentenceBody}*{checkSum}\r\n"; // string.Format("${0}*{1}\r\n", sentenceBody, checkSum);
		}

		public static bool TryParseNmeaSentence(string sentence, out NmeaMessage? message)
		{
			message = null;
			if (string.IsNullOrEmpty(sentence))
			{
				return false;
			}

			try
			{
				message = NmeaMessage.Parse(sentence);
				return message != null;
			}
			catch
			{
				return false;
			}
		}

		public static bool TryParseNmeaRmcSentenceAsHistoryGpsEvent(string sentence, out HistoryGpsEvent gpsEvent)
		{
			gpsEvent = null;
			if (string.IsNullOrEmpty(sentence))
			{
				return false;
			}

			try
			{
				var message = NmeaMessage.Parse(sentence);
				if (message == null || message.MessageType != RmcMessageType)
				{
					return false;
				}

				var rmc = message as Rmc;
				if (rmc == null)
				{
					return false;
				}

				gpsEvent = new HistoryGpsEvent()
				{
					Longitude = double.IsNaN(rmc.Longitude) ? null : Convert.ToDecimal(rmc.Longitude),
					Latitude = double.IsNaN(rmc.Latitude) ? null : Convert.ToDecimal(rmc.Latitude),
					Speed = double.IsNaN(rmc.Speed) ? null : Convert.ToDecimal(rmc.Speed) * KnotsToKmphFactor, // Convert knots to Kmph 
					Heading = double.IsNaN(rmc.Course) ? null : Convert.ToDecimal(rmc.Course),
					StartTimeValue = rmc.FixTime.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
				};

				return gpsEvent != null;
			}
			catch
			{
				gpsEvent = null;
				return false;
			}
		}

		public static string? ComposeDeviceIdNmeaSentence(string? profileName, string deviceId)
		{
			if (string.IsNullOrEmpty(profileName))
			{
				profileName = CradleDeviceProfileName; // Use Cradle device profile by default for backward compatibility
			}

			if (string.IsNullOrEmpty(deviceId))
			{
				return null;
			}

			string msgBody = string.Empty;
			switch (profileName)
			{
				case CradleDeviceProfileName:
					msgBody = $"PCPTI,{deviceId},85209,85209";
					break;
				default:
					return null;
			}

			string checkSum = GetChecksum(msgBody);
			return $"${msgBody}*{checkSum}\r\n";
		}

		public static string ComposeNmeaVtgSentence(HistoryGpsEvent gpsEvent, decimal? customSpeed = null)
		{
			decimal course = gpsEvent.Heading ?? 0;
			var speedToUse = customSpeed.HasValue ? customSpeed.Value : gpsEvent.Speed;
			string formattedCourse = course.ToString("0.00");
			string formattedSpeed = speedToUse?.ToString("0.00") ?? "0.00";
			string sentenceBody = string.Format("GPVTG,{0},T,,M,{1},N,,K,A", formattedCourse, formattedSpeed);
			string checkSum = GetChecksum(sentenceBody);
			return $"${sentenceBody}*{checkSum}\r\n"; // string.Format("${0}*{1}\r\n", sentenceBody, checkSum);
		}

		public static string ComposeNmeaGnsSentence(HistoryGpsEvent gpsEvent, DateTime fixTime)
		{
			// Format latitude and longitude
			string latitude = FormatLatitude(gpsEvent.Latitude);
			string longitude = FormatLongitude(gpsEvent.Longitude);

			// Compose GNGNS sentence
			// string gngnsSentenceBody = $"GNGNS,{fixTime:HHmmss},{latitude},{longitude},1,09,1.0,47.0,,*";
			string gngnsSentenceBody = $"GNGNS,{fixTime:HHmmss},{latitude},{longitude},AANN,16,0.7,75.1,-36.0,,,V";
			string checkSum = GetChecksum(gngnsSentenceBody);
			return $"${gngnsSentenceBody}*{checkSum}\r\n";
		}

		public static string ComposeNmeaGgaSentence(HistoryGpsEvent gpsEvent, DateTime fixTime)
		{
			// Format latitude and longitude
			string latitude = FormatLatitude(gpsEvent.Latitude);
			string longitude = FormatLongitude(gpsEvent.Longitude);
			// Compose GNGGA sentence
			// string gnggaSentenceBody = $"GNGGA,{fixTime:HHmmss},{latitude},{longitude},1,09,1.0,47.0,M,,*";
			string gnggaSentenceBody = $"GPGGA,{fixTime:HHmmss},{latitude},{longitude},1,09,0.7,{gpsEvent.Latitude??0:F1},M,-36.0,M,,";
			string checkSum = GetChecksum(gnggaSentenceBody);
			return $"${gnggaSentenceBody}*{checkSum}\r\n";
		}

		public static string ComposeNmeaGllSentence(HistoryGpsEvent gpsEvent, DateTime fixTime)
		{
			// Format latitude and longitude
			string latitude = FormatLatitude(gpsEvent.Latitude);
			string longitude = FormatLongitude(gpsEvent.Longitude);

			string gpgllSentenceBody = $"GPGLL,{latitude},{longitude},{fixTime:HHmmss.ff},A,A";
			string checkSum = GetChecksum(gpgllSentenceBody);
			return $"${gpgllSentenceBody}*{checkSum}\r\n";
		}

		public static string ConvertHistoryGpsEventToNmeaRmcSentence(HistoryGpsEvent gpsEvent, DateTime fixTime, decimal? customSpeed = null)
		{
			var speedToUse = customSpeed.HasValue ? customSpeed.Value : gpsEvent.Speed;
			var rmcSentence = BuildGprmcSentence(gpsEvent.Longitude, gpsEvent.Latitude, speedToUse, gpsEvent.Heading, fixTime);
			return rmcSentence;
		}

		public static string CreateMockCradleGpsDeviceId()
		{
			var guidPrefix = Guid.NewGuid().ToString().Split('-')[0];
			return $"MOK-{guidPrefix}";
		}

		private static string FormatLatitude(decimal? latitude)
		{
			if (!latitude.HasValue)
			{
				return ",";
			}

			string hemisphere = latitude >= 0 ? "N" : "S";
			decimal absLatitude = Math.Abs(latitude.Value);
			int degrees = (int)absLatitude;
			decimal minutes = (absLatitude - degrees) * 60;

			return string.Format("{0:00}{1:00.00000},{2}", degrees, minutes, hemisphere);
		}

		private static string FormatLongitude(decimal? longitude)
		{
			if (!longitude.HasValue)
			{
				return ",";
			}

			string hemisphere = longitude >= 0 ? "E" : "W";
			decimal absLongitude = Math.Abs(longitude.Value);
			int degrees = (int)absLongitude;
			decimal minutes = (absLongitude - degrees) * 60;

			return string.Format("{0:000}{1:00.00000},{2}", degrees, minutes, hemisphere);
		}

		private static string GetChecksum(string sentence)
		{
			byte checksum = 0;
			foreach (char c in sentence)
			{
				checksum ^= Convert.ToByte(c);
			}
			return checksum.ToString("X2");
		}

		private static Stream OpenAsyncReadOnlyFileStream(string filePath)
		{
			var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
			return fileStream;
		}
	}
}
