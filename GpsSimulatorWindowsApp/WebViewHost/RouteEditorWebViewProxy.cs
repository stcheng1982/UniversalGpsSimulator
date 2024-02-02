using DeviceId;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Helpers;
using GpsSimulatorWindowsApp.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace GpsSimulatorWindowsApp.WebViewHost
{
	[ComVisible(true)]
	public class RouteEditorWebViewProxy
	{

		private EditDrivingRouteViewModel ParentViewModel { get; set; }

		public event EventHandler<string> DrivingRouteSaved;

		public event EventHandler<List<HistoryGpsEvent>> AutoDrivingGpsEventsGenerated;

		public RouteEditorWebViewProxy(EditDrivingRouteViewModel parentVM)
		{
			ParentViewModel = parentVM;

		}

		public string VirtualDrivingRouteDataDirectoryPath { get; }

		public string[] GetAllRouteNamesFromLocalStorage()
		{
			var routes = VirtualDrivingDataHelper.FindAllDrivingRoutesInLocalStorage();
			return routes.Select(r => r.Name).ToArray();
		}

		public string? GetCurrentSelectedRouteName()
		{
			return ParentViewModel.SelectedRouteName;
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

		public string? SaveRouteDataToLocalStorage(string routeName, string routeDataJsonText, string previewImageDataUrl)
		{
			string? errorMessage = null;
			if (!IsRouteNameValid(routeName))
			{
				errorMessage = "Invalid Route Name, please use alphanumeric characters, underscore, dash, and space only.";
			}
			else
			{
				errorMessage = VirtualDrivingDataHelper.SaveRouteDataToLocalStorage(routeName, routeDataJsonText);

			}

			if (errorMessage != null)
			{
				MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}

			var previewImageBytes = ConvertImageDataUrlToBytes(previewImageDataUrl);
			if (previewImageBytes != null)
			{
				errorMessage = VirtualDrivingDataHelper.SaveRoutePreviewImageToLocalStorage(routeName, previewImageBytes);
			}

			if (errorMessage != null)
			{
				MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}

			return errorMessage;
		}

		public string? SaveGpsEventsOfVirtualDrivingRoutePLan(string routeName, string gpsEventsJsonStr)
		{
			List<HistoryGpsEvent> gpsEvents = null;
			try
			{
				gpsEvents = JsonSerializer.Deserialize<List<HistoryGpsEvent>>(gpsEventsJsonStr);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"GPS Events Deserialization Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return ex.Message;
			}

			AutoDrivingGpsEventsGenerated?.Invoke(this, gpsEvents);
			return null;
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

		private static bool IsRouteNameValid(string routeName)
		{
			if (string.IsNullOrWhiteSpace(routeName))
			{
				return false;
			}
			
			// Only allow alphanumeric characters, underscore, dash, and space
			var invalidChars = System.IO.Path.GetInvalidFileNameChars();
			foreach (var c in routeName)
			{
				if (invalidChars.Contains(c))
				{
					return false;
				}
			}

			return true;
		}

		private static byte[]? ConvertImageDataUrlToBytes(string imageDataUrl)
		{
			if (string.IsNullOrEmpty(imageDataUrl) || imageDataUrl.IndexOf(',') < 0)
			{
				return null;
			}

			var base64Data = imageDataUrl.Substring(imageDataUrl.IndexOf(',') + 1);
			var bytes = Convert.FromBase64String(base64Data);
			return bytes;
		}
	}
}
