using GpsSimulatorWindowsApp.DataType;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GpsSimulatorWindowsApp.Helpers
{
	public static class VirtualDrivingDataHelper
	{
		public const string VirtualDrivingRouteDataDirectoryName = "VirtualDrivingRouteData";

		public static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
		public static readonly string InvalidFileNameCharsText = string.Join(", ", InvalidFileNameChars.Select(c => $"'{c}'"));

		public static string VirtualDrivingRouteDataDirectoryPath
		{
			get
			{
				var appDataDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				return Path.Combine(appDataDirectoryPath, ApplicationConstants.ApplicationKey, VirtualDrivingRouteDataDirectoryName);
			}
		}

		public static string? ValidateRouteName(string? routeName)
		{
			if (string.IsNullOrEmpty(routeName))
			{
				return "Route name cannot be empty.";
			}

			if (routeName.IndexOfAny(InvalidFileNameChars) >= 0)
			{
				return $"Route name cannot contain invalid characters ({InvalidFileNameCharsText}).";
			}

			return null;
		}

		public static List<VirtualDrivingRoute> FindAllDrivingRoutesInLocalStorage()
		{
			var routeDataDirectoryPath = VirtualDrivingRouteDataDirectoryPath;
			if (!Directory.Exists(routeDataDirectoryPath))
			{
				Directory.CreateDirectory(routeDataDirectoryPath);
				return new List<VirtualDrivingRoute>();
			}

			var routeDataFiles = Directory.GetFiles(routeDataDirectoryPath, "*.json");
			var routes = routeDataFiles.Select(f => new VirtualDrivingRoute
			{
				Name = Path.GetFileNameWithoutExtension(f),
				RouteJsonDataFilePath = f
			}).ToList();
			return routes;
		}

		public static bool RouteNameExistsInLocalStorage(string routeName)
		{
			if (string.IsNullOrEmpty(routeName)) return false;

			var routeDataFilePath = Path.Combine(VirtualDrivingRouteDataDirectoryPath, $"{routeName}.json");
			return File.Exists(routeDataFilePath);
		}

		public static bool RenameRouteInLocalStorage(string oldRouteName, string newRouteName)
		{
			if (string.IsNullOrEmpty(oldRouteName) || string.IsNullOrEmpty(newRouteName))
			{
				return false;
			}

			var oldRouteDataFilePath = Path.Combine(VirtualDrivingRouteDataDirectoryPath, $"{oldRouteName}.json");
			if (!File.Exists(oldRouteDataFilePath))
			{
				MessageBox.Show($"Route '{oldRouteName}' does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}

			var newRouteDataFilePath = Path.Combine(VirtualDrivingRouteDataDirectoryPath, $"{newRouteName}.json");
			if (File.Exists(newRouteDataFilePath))
			{
				MessageBox.Show($"Route '{newRouteName}' already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}

			try
			{
				File.Move(oldRouteDataFilePath, newRouteDataFilePath);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}

			return true;
		}

		public static bool DeleteRouteFromLocalStorage(string routeName)
		{
			var routeDataFilePath = Path.Combine(VirtualDrivingRouteDataDirectoryPath, $"{routeName}.json");
			if (!File.Exists(routeDataFilePath))
			{
				return false;
			}

			try
			{
				File.Delete(routeDataFilePath);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}

			return true;
		}

		public static string? LoadRouteDataFromLocalStorage(string routeName)
		{
			string? jsonData = null;
			if (string.IsNullOrEmpty(routeName))
			{
				return jsonData;
			}

			var routeDataFilePath = System.IO.Path.Combine(VirtualDrivingRouteDataDirectoryPath, $"{routeName}.json");
			if (!File.Exists(routeDataFilePath))
			{
				return jsonData;
			}

			try
			{
				jsonData = File.ReadAllText(routeDataFilePath);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				jsonData = null;
			}

			return jsonData;
		}

		public static string? SaveRouteDataToLocalStorage(string routeName, string jsonData)
		{
			string? errorMessage = null;
			try
			{
				var routeDataFilePath = Path.Combine(VirtualDrivingRouteDataDirectoryPath, $"{routeName}.json");
				using (var fs = new FileStream(routeDataFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
				{
					using (var sw = new System.IO.StreamWriter(fs, Encoding.UTF8))
					{
						sw.Write(jsonData);
					}
				}
			}
			catch (Exception ex)
			{
				errorMessage = ex.Message;
			}

			return errorMessage;
		}

		public static string? SaveRoutePreviewImageToLocalStorage(string routeName, byte[] imageBytes)
		{
			string? errorMessage = null;
			try
			{
				var routePreviewImageFilePath = Path.Combine(VirtualDrivingRouteDataDirectoryPath, $"{routeName}.png");
				File.WriteAllBytes(routePreviewImageFilePath, imageBytes);
			}
			catch (Exception ex)
			{
				errorMessage = ex.Message;
			}
			return errorMessage;
		}

		public static string? GetRoutePreviewImageFilePath(string routeName)
		{
			string? routePreviewImageFilePath = null;
			if (string.IsNullOrEmpty(routeName))
			{
				return routePreviewImageFilePath;
			}
			routePreviewImageFilePath = Path.Combine(VirtualDrivingRouteDataDirectoryPath, $"{routeName}.png");
			if (!File.Exists(routePreviewImageFilePath))
			{
				routePreviewImageFilePath = null;
			}

			return routePreviewImageFilePath;
		}

		public static byte[]? LoadRoutePreviewImageFromLocalStorage(string routeName)
		{
			byte[]? imageBytes = null;
			if (string.IsNullOrEmpty(routeName))
			{
				return imageBytes;
			}
			var routePreviewImageFilePath = Path.Combine(VirtualDrivingRouteDataDirectoryPath, $"{routeName}.png");
			if (!File.Exists(routePreviewImageFilePath))
			{
				return imageBytes;
			}
			try
			{
				imageBytes = File.ReadAllBytes(routePreviewImageFilePath);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				imageBytes = null;
			}
			return imageBytes;
		}
	}
}
