using CsvHelper.Configuration;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GpsSimulatorWindowsApp.Helpers
{
	public class ExcelAndCsvDataHelper
	{
		public static async Task<string?> TryValidatingCsvFileAsync(string csvFilePath)
		{
			try
			{
				var events = await GetHistoryGpsEventsFromCsvFileAsync(csvFilePath);
				if (events?.Any() == true)
				{
					return null;
				}

				return $"No Gps Events detected in file '{csvFilePath}'";
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}

		public static async Task<List<HistoryGpsEvent>> GetHistoryGpsEventsFromCsvFileAsync(string csvFilePath)
		{
			if (string.IsNullOrEmpty(csvFilePath) || !File.Exists(csvFilePath))
			{
				throw new ArgumentNullException(nameof(csvFilePath));
			}

			var events = new List<HistoryGpsEvent>();
			bool hasHeaderLine = false;

			using var headerDetectStream = OpenAsyncReadOnlyFileStream(csvFilePath);
			using (var sr = new StreamReader(headerDetectStream))
			{
				var firstLine = await sr.ReadLineAsync();
				hasHeaderLine = !string.IsNullOrEmpty(firstLine)
					&& firstLine.Contains("Longitude", StringComparison.Ordinal) && firstLine.Contains("Latitude", StringComparison.Ordinal)
					&& firstLine.Contains("Speed", StringComparison.Ordinal) && firstLine.Contains("Heading", StringComparison.Ordinal)
					&& firstLine.Contains("StartTime", StringComparison.Ordinal);
			}

			using var fs = OpenAsyncReadOnlyFileStream(csvFilePath);
			using (var sr = new StreamReader(fs))
			{
				var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
				{
					HasHeaderRecord = hasHeaderLine,
				};
				using (var csvReader = new CsvHelper.CsvReader(sr, csvConfig))
				{
					if (hasHeaderLine)
					{
						csvReader.Context.RegisterClassMap<HistoryGpsEventNameCsvClassMap>();
						// Read the header line
						//await csvReader.ReadAsync();
						//csvReader.ReadHeader();
					}
					else
					{
						csvReader.Context.RegisterClassMap<HistoryGpsEventIndexCsvClassMap>();
					}

					await foreach (var gpsEvent in csvReader.GetRecordsAsync<HistoryGpsEvent>())
					{
						if (gpsEvent != null && gpsEvent.Longitude.HasValue && gpsEvent.Latitude.HasValue)
						{
							events.Add(gpsEvent);
						}
					}
				}
			}

			return events;
		}

		public static async Task<(bool exported, string error)> SaveHistoryGpsEventsAsCsvFile(string csvFilePath, List<HistoryGpsEvent> historyGpsEvents)
		{
			try
			{
				if (historyGpsEvents?.Any() != true)
				{
					return (false, "Invalid History Events");
				}

				var dirPath = Path.GetDirectoryName(csvFilePath);
				if (!Directory.Exists(dirPath))
				{
					Directory.CreateDirectory(dirPath);
				}

				using (var sw = new StreamWriter(csvFilePath, false, Encoding.UTF8))
				{
					using (var csvWriter = new CsvHelper.CsvWriter(sw, CultureInfo.InvariantCulture))
					{
						csvWriter.Context.RegisterClassMap<HistoryGpsEventNameCsvClassMap>();
						csvWriter.WriteHeader<HistoryGpsEvent>();
						await csvWriter.NextRecordAsync();
						
						foreach (var gpsEvent in historyGpsEvents)
						{
							if (gpsEvent != null && gpsEvent.Longitude.HasValue && gpsEvent.Latitude.HasValue)
							{
								csvWriter.WriteRecord<HistoryGpsEvent>(gpsEvent);
								await csvWriter.NextRecordAsync();
							}
						}
					}
				}

				return (true, csvFilePath);
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error in SaveHistoryGpsEventsAsCsvFile: {ex}");
				return (false, ex.Message);
			}
		}

		private static Stream OpenAsyncReadOnlyFileStream(string filePath)
		{
			var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
			return fileStream;
		}
	}

	public class HistoryGpsEventNameCsvClassMap: ClassMap<HistoryGpsEvent>
	{
		public HistoryGpsEventNameCsvClassMap()
		{
			Map(evt => evt.Longitude).Name("Longitude");
			Map(evt => evt.Latitude).Name("Latitude");
			Map(evt => evt.Speed).Name("Speed");
			Map(evt => evt.Heading).Name("Heading");
			Map(evt => evt.StartTimeValue).Name("StartTime");
		}
	}

	public class HistoryGpsEventIndexCsvClassMap : ClassMap<HistoryGpsEvent>
	{
		public HistoryGpsEventIndexCsvClassMap()
		{
			Map(evt => evt.Longitude).Index(0);
			Map(evt => evt.Latitude).Index(1);
			Map(evt => evt.Speed).Index(2);
			Map(evt => evt.Heading).Index(3);
			Map(evt => evt.StartTimeValue).Index(4);
		}
	}
}
