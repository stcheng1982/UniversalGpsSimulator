using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace GpsSimulatorWindowsApp.Helpers
{
	public class GPXDataHelper
	{
		public const decimal HardcodedElevation = 10m;
		public const string RelativePathOfGPXTemplateFile = @"Data\GPX\GPX_TEMPLATE.gpx";

		public static async Task<(bool exported, string error)> SaveHistoryGpsEventsAsGPXFile(string gpxFilePath, List<HistoryGpsEvent> historyGpsEvents, string? name = null, string? keywords = null)
		{
			try
			{
				if (historyGpsEvents?.Any() != true)
				{
					return (false, "Invalid History Events");
				}

				var dirPath = Path.GetDirectoryName(gpxFilePath);
				if (!Directory.Exists(dirPath))
				{
					Directory.CreateDirectory(dirPath);
				}

				var gpxDoc = await ConvertHistoryGpsEventsToGPXDocumentAsync(historyGpsEvents).ConfigureAwait(false);
				using (var sw = new StreamWriter(gpxFilePath, false, Encoding.UTF8))
				{
					await gpxDoc.SaveAsync(sw, SaveOptions.None, CancellationToken.None);
				}

				return (true, gpxFilePath);
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error in SaveHistoryGpsEventsAsGPXFile: {ex}");
				return (false, ex.Message);
			}
		}

		public static async Task<XDocument> ConvertHistoryGpsEventsToGPXDocumentAsync(List<HistoryGpsEvent> historyGpsEvents, string? name = null, string? keywords = null)
		{
			var gpxTemplateFilePath = Path.Combine(AppContext.BaseDirectory, RelativePathOfGPXTemplateFile);
			XDocument gpxDoc;
			using (var fs = File.OpenRead(gpxTemplateFilePath))
			{
				gpxDoc = await XDocument.LoadAsync(fs, LoadOptions.None, CancellationToken.None);
			}

			var defaultNS = gpxDoc.Root.Name.Namespace.NamespaceName;
			var metadataXName = XName.Get("metadata", defaultNS);
			var metadataElement = gpxDoc.Root.Descendants(metadataXName).FirstOrDefault();
			if (metadataElement != null)
			{
				// Update name, time and keywords in metadata
				var nameValue = !string.IsNullOrEmpty(name) ? name : "Track from History Gps Events";
				var timeValue = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ");
				var keywordsValue = !string.IsNullOrEmpty(keywords) ? keywords : "History Gps Events, GPX";

				var nameElement = metadataElement.Descendants(XName.Get("name", defaultNS)).FirstOrDefault();
				if (nameElement != null)
				{
					nameElement.Value = nameValue;
				}

				var timeElement = metadataElement.Descendants(XName.Get("time", defaultNS)).FirstOrDefault();
				if (timeElement != null)
				{
					timeElement.Value = timeValue;
				}

				var keywordsElement = metadataElement.Descendants(XName.Get("keywords", defaultNS)).FirstOrDefault();
				if (keywordsElement != null)
				{
					keywordsElement.Value = keywordsValue;
				}
			}

			var trkElement = gpxDoc.Root.Descendants().FirstOrDefault(elm => elm.Name.LocalName == "trk");
			if (trkElement == null)
			{
				trkElement = new XElement("trk");
				gpxDoc.Root.Add(trkElement);
			}


			var trkSegElement = trkElement.Descendants().FirstOrDefault(elm => elm.Name.LocalName == "trkseg");
			if (trkSegElement == null)
			{
				trkSegElement = new XElement("trkseg");
				trkElement.Add(trkSegElement);
			}

			trkSegElement.RemoveAll(); // clear all child elements

			foreach (var gpsEvent in historyGpsEvents)
			{
				var trkPtElement = new XElement(XName.Get("trkpt", trkSegElement.GetDefaultNamespace().NamespaceName));

				trkPtElement.SetAttributeValue("lat", gpsEvent.Latitude ?? 0);
				trkPtElement.SetAttributeValue("lon", gpsEvent.Longitude ?? 0);
				var timeElement = new XElement(XName.Get("time", trkSegElement.GetDefaultNamespace().NamespaceName), gpsEvent.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"));
				trkPtElement.Add(timeElement);
				var eleElement = new XElement(XName.Get("ele", trkSegElement.GetDefaultNamespace().NamespaceName), HardcodedElevation);
				trkPtElement.Add(eleElement);

				var speedValue = gpsEvent.Speed.HasValue ? gpsEvent.Speed.Value.ToString("0.0") : "0";
				var speedElement = new XElement(XName.Get("speed", trkSegElement.GetDefaultNamespace().NamespaceName), speedValue);
				trkPtElement.Add(speedElement);

				var courseValue = gpsEvent.Heading.HasValue ? gpsEvent.Heading.Value.ToString("0.00") : "0";
				var courseElement = new XElement(XName.Get("course", trkSegElement.GetDefaultNamespace().NamespaceName), courseValue);
				trkPtElement.Add(courseElement);

				trkSegElement.Add(trkPtElement);
			}

			return gpxDoc;
		}

	}
}
