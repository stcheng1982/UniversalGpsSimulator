using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GpsSimulatorWindowsApp.Logging
{
	public static class SerilogSetup
	{
		public const string AppLogDirectoryName = "logs";
		public const string AppLogFileName = "UniversalGpsSimulator_.log";
		public const string DefaultLogTemplate = "{Timestamp:u} [{Level:u3}] {MachineName} ~{ThreadId} {SourceContext} {Message}{NewLine}{Exception}";

		public static void SetupSerilog()
		{
			try
			{
				var logFilePath = Path.Combine(ApplicationLogDataDirectoryPath, AppLogFileName);


				Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Information()
				.WriteTo.Console()
				.WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 10, outputTemplate: DefaultLogTemplate)
				.Enrich.FromLogContext()
				.Enrich.WithThreadId()
				.CreateLogger();

				Log.Information("Serilog registered for Universal GPS Simulator.");
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		public static string ApplicationLogDataDirectoryPath
		{
			get
			{
				var appDataDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				return Path.Combine(appDataDirectoryPath, ApplicationConstants.ApplicationKey, AppLogDirectoryName);
			}
		}
	}
}
