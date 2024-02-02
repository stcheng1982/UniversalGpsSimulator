using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.Logging
{
	using Serilog;
	using System;

	public static class LogHelper
	{
		private const string DEFAULT_NAME = "GpsSimulatorWindowsApp";

		public static void Info(string message)
		{
			GetLogger().Information(message);
		}

		public static void Info(string message, Exception ex)
		{
			GetLogger().Information(message, ex);
		}

		public static void Debug(string message)
		{
			GetLogger().Debug(message);
		}

		public static void Debug(string message, Exception ex)
		{
			GetLogger().Debug(message, ex);
		}

		public static void Warn(string message)
		{
			GetLogger().Warning(message);
		}

		public static void Warn(string message, Exception ex)
		{
			GetLogger().Warning(message, ex);
		}

		public static void Error(string message)
		{
			GetLogger().Error(message);
		}

		public static void Error(Exception ex)
		{
			GetLogger().Error(ex, string.Empty);
		}

		public static void Error(string message, Exception ex)
		{
			GetLogger().Error(ex, message);
		}

		private static Serilog.ILogger GetLogger()
		{
			System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
			string typeName = stackTrace?.GetFrame(2)?.GetMethod()?.ReflectedType?.FullName ?? DEFAULT_NAME;
			var logger = Log.ForContext("SourceContext", typeName);
			return logger;
		}
	}
}
