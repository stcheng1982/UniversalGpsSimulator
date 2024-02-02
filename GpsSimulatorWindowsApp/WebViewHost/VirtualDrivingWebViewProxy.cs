using DeviceId;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Helpers;
using GpsSimulatorWindowsApp.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.WebViewHost
{
	[ComVisible(true)]
	public class VirtualDrivingWebViewProxy
	{
		private MainWindowViewModel ViewModel { get; set; }

		public VirtualDrivingWebViewProxy(MainWindowViewModel mainWindowViewModel)
		{
			ViewModel = mainWindowViewModel;
		}

		public string? LoadRouteDataFromLocalStorage(string routeName)
		{
			string? jsonData = null;
			if (string.IsNullOrEmpty(routeName))
			{
				return jsonData;
			}

			jsonData = VirtualDrivingDataHelper.LoadRouteDataFromLocalStorage(routeName);

			return jsonData;
		}

		public void PublishGeolocationOfDrivingVehicle(decimal longitude, decimal latitude, decimal speed, decimal heading, long jstime)
		{
			//Debug.WriteLine($"longitude: {longitude}, latitude: {latitude}, speed: {latitude}, heading: {heading}, jstime: {jstime}");
			
			var eventTime = DateTimeOffset.FromUnixTimeMilliseconds(jstime).UtcDateTime; // Convert js time to DateTime
			var newGpsEvent = new HistoryGpsEvent()
			{
				Longitude = longitude,
				Latitude = latitude,
				Speed = speed,
				Heading = heading,
				StartTimeValue = eventTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
			};

			ViewModel.NotifyReceivedVirtualDrivingGpsEvent(newGpsEvent);
		}

		public void MarkFullConductDrivingAsCompleted()
		{
			// Stop the Virtual Driving if it is running 
			if (ViewModel.VirtualDrivingRealtimeInput.IsDriving)
			{
				ViewModel.StartOrStopVirtualDriving();
			}
		}

		public string GetDeviceInformationAsJson()
		{
			var deviceId = GetDeviceIdentifier();

			var deviceInfo = new PatrolfinderDeviceInfo()
			{
				MachineName = Environment.MachineName,
				OSVersion = Environment.OSVersion.ToString(),
				TimeZoneName = TimeZoneInfo.Local.DisplayName,
				DeviceIdentifier = deviceId,
			};

			var deviceInfoJsonText = JsonSerializer.Serialize(deviceInfo);
			return deviceInfoJsonText;
		}

		public string GetDeviceIdentifier()
		{
			//IDeviceIdFormatter formatter = new HashDeviceIdFormatter(() => SHA256.Create(), new Base32ByteArrayEncoder("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".Substring(0, 32)));
			var deviceId = new DeviceIdBuilder()
				.AddMachineName()
				.AddOsVersion()
				.OnWindows(windows => windows
					.AddProcessorId()
					.AddMotherboardSerialNumber()
					.AddSystemDriveSerialNumber()
				)
				//.UseFormatter(formatter)
				.ToString();

			return deviceId;
		}

		public string GetUserSessionInformation()
		{
			var machineName = Environment.MachineName;
			var userName = Environment.UserName;
			var timeZoneName = TimeZoneInfo.Local.DisplayName;
			var userSessionInfo = new AppUserSessionInfo
			{
				MachineName = machineName,
				OSVersion = Environment.OSVersion.ToString(),
				UserName = userName,
				TimeZone = timeZoneName,
			};

			return JsonSerializer.Serialize(userSessionInfo);
		}
	}
}
