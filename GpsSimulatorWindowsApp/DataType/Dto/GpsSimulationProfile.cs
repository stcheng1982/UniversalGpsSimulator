using GpsSimulatorWindowsApp.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.DataType
{
	public class GpsSimulationProfile
	{
		public GpsSimulationProfile()
		{
			SendToVirtualSerialPort = true;
			SerialPortName = "COM3";
			SerialPortBaudRate = 9600;

			EnableLocalTcpHost = false;
			LocalTcpHostInfo = string.Empty;
			DeviceIdForLocalTcpHost = string.Empty;

			SendToServerViaUdp = false;
			ServerUdpHostInfo = string.Empty;
			DeviceIdForServerUdp = string.Empty;

			SendToAndroidViaAdb = false;
			AndroidAdbSerialNumber = string.Empty;

			SendToAndroidViaUdp = false;
			AndroidUdpHostInfo = string.Empty;

			SendToIOSDevice = false;
			IOSDeviceUdid = string.Empty;

			MultiDeviceSimulationTarget = MultiDeviceSimulationTargetType.None;
			PlusDbConnectionString = string.Empty;
			PlusDatabaseId = 0;
			PlusGpsVendorId = 0;
			PlusGpsVehicles = new List<PlusGpsVehicleItem>();


			// Event DataSource related settings
			ResetToDefaultLocalFileGpsEventSource(); // Set as LocalFile EventSource by default

			ReverseGpsEventsFromWebResource = false;
			UseMockSpeed = false;
			MinMockSpeed = 20;
			MaxMockSpeed = 40;
			SendInvalidLocationOnPause = false;

			// Misc settings
			SendDataDelayInMillionSeconds = 1000;

		}

		/// <summary>
		/// If mock gps events will be sent to virtual serial port target
		/// </summary>
		public bool? SendToVirtualSerialPort { get; set; }

		public string SerialPortName { get; set; }

		public int SerialPortBaudRate { get; set; }

		public bool? EnableLocalTcpHost { get; set; }

		public string LocalTcpHostInfo { get; set; }

		public string DeviceIdForLocalTcpHost { get; set; }

		public NmeaSentencePlaybackOptions? LocalTcpHostNmeaOptions { get; set; }

		public bool? SendToServerViaUdp { get; set; }

		public string ServerUdpHostInfo { get; set; }

		public string DeviceIdForServerUdp { get; set; }

		public NmeaSentencePlaybackOptions? ServerUdpNmeaOptions { get; set; }

		/// <summary>
		/// If mock gps events will be sent to Android device via ADB
		/// </summary>
		public bool? SendToAndroidViaAdb { get; set; }

		/// <summary>
		/// Serial number of Android device. e.g. 988a1a4d4c4e4f4d
		/// </summary>
		public string AndroidAdbSerialNumber { get; set; }

		/// <summary>
		/// If mock gps events will be sent to Android device via UDP
		/// </summary>
		public bool? SendToAndroidViaUdp { get; set; }

		/// <summary>
		/// IP + Port". e.g. 127.0.0.1:9166
		/// </summary>
		public string AndroidUdpHostInfo { get; set; }

		public bool? SendToIOSDevice { get; set; }

		public string IOSDeviceUdid { get; set; }

		/// <summary>
		/// Target Type for Multi-Device Simulation
		/// </summary>
		public MultiDeviceSimulationTargetType? MultiDeviceSimulationTarget { get; set; }

		// public bool? SendToPlusDb { get; set; }

		public string PlusDbConnectionString { get; set; }

		public short PlusDatabaseId { get; set; }

		public int PlusGpsVendorId { get; set; }

		public List<PlusGpsVehicleItem> PlusGpsVehicles { get; set; }

		public string PatrolfinderMultiDeviceServerUdpHostInfo { get; set; }

		public List<PatrolfinderServerUdpDeviceItem> PatrolfinderServerUdpDeviceItems { get; set; }

		public int SendDataDelayInMillionSeconds { get; set; }

		public GpsDataSourceType DataSource { get; set; }

		public LocalGpsDataFileType? LocalGpsDataFileType { get; set; }

		public string? LocalGpsDataFilePath { get; set; }

		[Obsolete("NmeaLogFilePath is deprecated, please use LocalGpsDataFilePath instead.")]
		public string? NmeaLogFilePath { get; set; }

		public string? WebGpsDataResourceUrl { get; set; }

		public HttpRequestOptionsForWebGpsEventSource? WebGpsDataRequestOptions { get; set; }

		public bool? UsePFDeviceHistoryAsWebEventSource { get; set; }

		[Obsolete("Use WebGpsDataResourceUrl instead.")]
		public string? HistoryGpsEventDataApiUrl { get; set; }

		public string HistoryDeviceId { get; set; }

		public DateTime HistoryStartDateTime { get; set; }

		public DateTime HistoryEndDateTime { get; set; }

		public bool? ReverseGpsEventsFromWebResource { get; set; }

		public bool UseMockSpeed { get; set; }

		public decimal MinMockSpeed { get; set; }

		public decimal MaxMockSpeed { get; set; }

		public bool SendInvalidLocationOnPause { get; set; }

		public string GetSingleDeviceSimulationTitle()
		{
			var targetAbbreviations = new List<string>();
			if (SendToVirtualSerialPort == true)
			{
				targetAbbreviations.Add("SP");
			}

			if (SendToServerViaUdp == true)
			{
				targetAbbreviations.Add("SU");
			}

			if (SendToAndroidViaAdb == true)
			{
				targetAbbreviations.Add("AA");
			}

			if (SendToAndroidViaUdp == true)
			{
				targetAbbreviations.Add("AU");
			}

			var targetDeviceFlag = $"[{string.Join("|", targetAbbreviations)}]";
			if (DataSource == GpsDataSourceType.WebEventSource)
			{
				string reversedFlag = ReverseGpsEventsFromWebResource == true ? ", Reversed" : string.Empty;
				if (UsePFDeviceHistoryAsWebEventSource != false)
				{
					return $"{targetDeviceFlag} Device: {HistoryDeviceId}, Time: ({HistoryStartDateTime.ToString("yyyy-MM-dd HH:mm:ss")} - {HistoryEndDateTime.ToString("yyyy-MM-dd HH:mm:ss")}){reversedFlag}";
				}
				else
				{
					return $"{targetDeviceFlag} General Web Resource {WebGpsDataResourceUrl}";
				}
			}
			else
			{
				var fileType = LocalGpsDataFileType ?? GpsSimulatorWindowsApp.LocalGpsDataFileType.NMEA;
				var fileName = !string.IsNullOrEmpty(LocalGpsDataFilePath) ? System.IO.Path.GetFileName(LocalGpsDataFilePath) : string.Empty;
				return $"{targetDeviceFlag} *{fileType.ToString()}* {fileName}";
			}
		}

		public string GetPlusSimulationTitle()
		{
			var currentMultiDeviceTarget = MultiDeviceSimulationTarget ?? MultiDeviceSimulationTargetType.None;
			if (currentMultiDeviceTarget == MultiDeviceSimulationTargetType.None)
			{
				return "[None]";
			}
			else if (currentMultiDeviceTarget == MultiDeviceSimulationTargetType.PlusDatabase)
			{
				var titleBuffer = new StringBuilder();
				titleBuffer.Append("[PLUS] ");
				titleBuffer.Append($"{PlusGpsVehicles?.Count ?? 0} Vehicles");

				titleBuffer.Append($", VendorId: {PlusGpsVendorId}, DBID: {PlusDatabaseId}, ConnStr: {PlusDbConnectionString}");
			
				return titleBuffer.ToString();
			}
			else if (currentMultiDeviceTarget == MultiDeviceSimulationTargetType.PatrolfinderServerUdp)
			{
				var titleBuffer = new StringBuilder();
				titleBuffer.Append("[PF UDP] ");
				titleBuffer.Append($"{PatrolfinderServerUdpDeviceItems?.Count ?? 0} Devices");
				titleBuffer.Append($", Host: {PatrolfinderMultiDeviceServerUdpHostInfo}");
			
				return titleBuffer.ToString();
			}

			return string.Empty;
		}

		public void ResetToDefaultWebGpsEventSource()
		{
			DataSource = GpsDataSourceType.WebEventSource;
			UsePFDeviceHistoryAsWebEventSource = true;
			WebGpsDataResourceUrl = GpsEventPlaybackDataHelper.ComposePatrolfinderHistoryEventsApiUrl(
				GpsEventPlaybackDataHelper.DefaultPatrolfinderHistoryEventsApiBaseUrl,
				GpsEventPlaybackDataHelper.DefaultPatrolfinderHistoryEventsDeviceId,
				GpsEventPlaybackDataHelper.DefaultPatrolfinderHistoryEventsStartTimeValue,
				GpsEventPlaybackDataHelper.DefaultPatrolfinderHistoryEventsEndTimevalue);
			WebGpsDataRequestOptions = HttpRequestOptionsForWebGpsEventSource.DefaultHttpRequestOptions;

			HistoryDeviceId = GpsEventPlaybackDataHelper.DefaultPatrolfinderHistoryEventsDeviceId;
			HistoryStartDateTime = DateTime.Parse(GpsEventPlaybackDataHelper.DefaultPatrolfinderHistoryEventsStartTimeValue);
			HistoryEndDateTime = DateTime.Parse(GpsEventPlaybackDataHelper.DefaultPatrolfinderHistoryEventsEndTimevalue);

			ReverseGpsEventsFromWebResource = false;

			LocalGpsDataFileType = null;
			LocalGpsDataFilePath = string.Empty;
		}

		public void ResetToDefaultLocalFileGpsEventSource()
		{
			DataSource = GpsDataSourceType.LocalFileEventSource;
			LocalGpsDataFileType = GpsSimulatorWindowsApp.LocalGpsDataFileType.NMEA; // Default one
			LocalGpsDataFilePath = NmeaDataHelper.DefaultSampleNmeaFilePath;
			ReverseGpsEventsFromWebResource = false;

			WebGpsDataResourceUrl = string.Empty;
			WebGpsDataRequestOptions = null;
		}
	
		public GpsSimulationProfile DeepCopy()
		{
			// Use Json Serialization to produce a deepcopy of current GpsSimulationProfile
			var jsonStr = JsonSerializer.Serialize(this);
			var copiedInstance = JsonSerializer.Deserialize<GpsSimulationProfile>(jsonStr);
			return copiedInstance;
		}
	}

	public class PlusGpsVehicleItem
	{
		public string VehicleGpsId { get; set; }

		public LocalGpsDataFileType GpsDataFileType { get; set; }

		public string GpsDataFilePath { get; set; }
	}

	public class PatrolfinderServerUdpDeviceItem
	{
		public string DeviceId { get; set; }

		public LocalGpsDataFileType GpsDataFileType { get; set; }

		public string GpsDataFilePath { get; set; }

		public NmeaSentencePlaybackOptions? NmeaOptions { get; set; }
	}
}
