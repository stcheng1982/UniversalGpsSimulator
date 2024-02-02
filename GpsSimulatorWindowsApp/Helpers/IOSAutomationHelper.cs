using GpsSimulatorWindowsApp.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GpsSimulatorWindowsApp.Helpers
{
	public class IOSAutomationHelper
	{
		public const string ExternalToolsDirName = "ExtTools";

		public const string ZipArchiveNameOfLibimobileTool = "libimobile.zip";

		public const string RelativeDirPathOfLibimobileTool = @"ExtTools\libimobile";

		public const string LibimobilesetlocationCmdName = "idevicesetlocation.exe";

		public static void EnsureLibimobileToolExists()
		{
			try
			{
				var fullPathOfExtToolsDir = Path.Combine(AppContext.BaseDirectory, ExternalToolsDirName);
				var fullPathOfToolDir = Path.Combine(AppContext.BaseDirectory, RelativeDirPathOfLibimobileTool);
				if (!Directory.Exists(fullPathOfToolDir))
				{
					var zipArchivePath = Path.Combine(AppContext.BaseDirectory, ExternalToolsDirName, ZipArchiveNameOfLibimobileTool);
					if (!File.Exists(zipArchivePath))
					{
						MessageBox.Show($"External Tool bundle {ZipArchiveNameOfLibimobileTool} not found.");
						return;
					}

					// Extract zipArchivePath to fullPathOfExtToolsDir
					ZipFile.ExtractToDirectory(zipArchivePath, fullPathOfExtToolsDir);
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error during EnsureLibimobileToolExists: {ex.Message}");
			}
		}

		public static void TrySettingMockLocationToIOSDevice(string deviceUdid, decimal longitude, decimal latitude)
		{
			var fullPathOfToolDir = Path.Combine(AppContext.BaseDirectory, RelativeDirPathOfLibimobileTool);
			var fullPathOfToolExeutable = Path.Combine(fullPathOfToolDir, LibimobilesetlocationCmdName);
			if (!File.Exists(fullPathOfToolExeutable))
			{
				LogHelper.Error($"{fullPathOfToolExeutable} is not found.");
				return;
			}

			var cmdStr = "cmd";
			var udidArg = string.IsNullOrEmpty(deviceUdid) ? string.Empty : $"-u {deviceUdid}";
			var cmdArgs = $"/c {LibimobilesetlocationCmdName} {udidArg} -- {latitude} {longitude}";

			var startInfo = new ProcessStartInfo(cmdStr, cmdArgs)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				LoadUserProfile = false,
				RedirectStandardOutput = false,
				RedirectStandardError = false,
				WorkingDirectory = fullPathOfToolDir,
			};

			var p = Process.Start(startInfo);
			p.WaitForExitAsync().ContinueWith(t =>
			{
				Exception exp = t.Exception;
				if (exp != null)
				{
					LogHelper.Error($"{LibimobilesetlocationCmdName} execution error: {exp}");
				}
			});
		}

		public static void TryResettingMockLocationService()
		{
			var fullPathOfToolDir = Path.Combine(AppContext.BaseDirectory, RelativeDirPathOfLibimobileTool);
			var fullPathOfToolExeutable = Path.Combine(fullPathOfToolDir, LibimobilesetlocationCmdName);
			if (!File.Exists(fullPathOfToolExeutable))
			{
				LogHelper.Error($"{fullPathOfToolExeutable} is not found.");
				return;
			}

			var cmdStr = "cmd";
			var cmdArgs = $"/c {LibimobilesetlocationCmdName} reset";

			var startInfo = new ProcessStartInfo(cmdStr, cmdArgs)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				LoadUserProfile = false,
				RedirectStandardOutput = false,
				RedirectStandardError = false,
				WorkingDirectory = fullPathOfToolDir,
			};

			var p = Process.Start(startInfo);
			p.WaitForExitAsync().ContinueWith(t =>
			{
				Exception exp = t.Exception;
				if (exp != null)
				{
					LogHelper.Error($"{LibimobilesetlocationCmdName} execution error: {exp}");
				}
			});
		}
	}
}
