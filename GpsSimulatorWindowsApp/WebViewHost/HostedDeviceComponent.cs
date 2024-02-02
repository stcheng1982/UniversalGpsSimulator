using DeviceId;
using DeviceId.Encoders;
using DeviceId.Formatters;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Helpers;
using GpsSimulatorWindowsApp.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.WebViewHost
{
	[ComVisible(true)]
	public class HostedDeviceComponent
	{
		private MainWindowViewModel ViewModel { get; set; }

		public HostedDeviceComponent(MainWindowViewModel mainWindowViewModel)
		{
			ViewModel = mainWindowViewModel;
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
			var simulatorAppVersion = ClickOnceVersionHelper.GetVersionSummary();
			var userSessionInfo = new AppUserSessionInfo
			{
				MachineName = machineName,
				OSVersion = Environment.OSVersion.ToString(),
				UserName = userName,
				TimeZone = timeZoneName,
				SimulatorAppVersion = simulatorAppVersion,
			};

			return JsonSerializer.Serialize(userSessionInfo);
		}
	}
}
