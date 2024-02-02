using Microsoft.Data.SqlClient;
using GpsSimulatorWindowsApp.DataType;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GpsSimulatorWindowsApp.Logging;

namespace GpsSimulatorWindowsApp.Helpers
{
	public class PlusDbQueryHelper
	{
		public const string TrustServerCertificateConnectionOption = "TrustServerCertificate=True;";
		public const string ConnectionTimeoutKey = "Connection Timeout";

		public static bool IsPlusDbConnectable(string connStr)
		{
			bool result = false;
			try
			{
				connStr = EnsureTrustServerCertificateInConnectionString(connStr);
				connStr = SetConnectionTimeoutInConnectionString(connStr, 10);

				using (var conn = new SqlConnection(connStr))
				{
					conn.Open();
					result = true;
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error($"IsPlusDbConnectable: {ex}");
				result = false;
			}

			return result;
		}

		public static List<PlusDataSource> GetDataSourceList(string connStr)
		{
			var result = new List<PlusDataSource>();

			try
			{
				connStr = EnsureTrustServerCertificateInConnectionString(connStr);
				var querySql = "SELECT [DBID], [Name] FROM [DBINFO]";
				foreach (var fieldsAccessor in SqlDbAccessHelper.EnumerateResultSet(connStr, querySql))
				{
					var dbid = fieldsAccessor.GetFieldValue<short>(0);
					var name = fieldsAccessor.GetFieldValue<string>(1);
					var dataSource = new PlusDataSource { Id = dbid, Name = name };
					result.Add(dataSource);
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error($"GetDataSourceList: {ex}");
			}

			return result;
		}

		public static List<PlusGpsVendor> GetGpsVendorList(string connStr)
		{
			var result = new List<PlusGpsVendor>();

			try
			{
				connStr = EnsureTrustServerCertificateInConnectionString(connStr);

				var querySql = "SELECT [ID], [Name] FROM [Vendor] WHERE [Enabled] = 1";
				foreach (var fieldsAccessor in SqlDbAccessHelper.EnumerateResultSet(connStr, querySql))
				{
					var vendorId = fieldsAccessor.GetFieldValue<int>(0);
					var vendorName = fieldsAccessor.GetFieldValue<string>(1);
					var gpsVendor = new PlusGpsVendor { Id = vendorId, Name = vendorName };
					result.Add(gpsVendor);
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex.Message);
			}

			return result;
		}

		public static TimeZoneInfo GetClientTimeZone(string connStr)
		{
			string timeZoneId = "Eastern Standard Time";
			try
			{
				connStr = EnsureTrustServerCertificateInConnectionString(connStr);

				var sqlQuery = "SELECT [TimeZone] from [ClientConfig]";
				foreach (var fieldsAccessor in SqlDbAccessHelper.EnumerateResultSet(connStr, sqlQuery))
				{
					var timeZoneString = fieldsAccessor.GetFieldValue<string>(0);
					if (!string.IsNullOrEmpty(timeZoneString))
					{
						timeZoneId = ExtractTimeZoneIdFromTFTimeZoneString(timeZoneString);
					}
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex.Message);
			}

			return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
		}

		public static List<string> GetUniqueVehicleGpsIdList(string connStr, short dataSourceId)
		{
			var result = new List<string>();

			try
			{
				connStr = EnsureTrustServerCertificateInConnectionString(connStr);

				var querySql = $"SELECT DISTINCT [GPSID] FROM [Vehicle] WHERE [DBID] = {dataSourceId} AND [GPSID] IS NOT NULL";
				foreach (var fieldsAccessor in SqlDbAccessHelper.EnumerateResultSet(connStr, querySql))
				{
					var gpsId = fieldsAccessor.GetFieldValue<string>(0);
					result.Add(gpsId);
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error($"GetUniqueVehicleGpsIdList: {ex}");
			}

			return result;
		}

		public static int GetMockGpsEventTypeId(string connStr, int vendorId)
		{
			int vendorEventTypeId = -1;
			try
			{
				connStr = EnsureTrustServerCertificateInConnectionString(connStr);

				var querySql = $@"
SELECT
 vet.[ID] AS [VendorEventTypeID]
, et.[EventTypeName]
FROM [VendorEventType] vet
INNER JOIN [EventType] et ON vet.[EventTypeID] = et.[ID]
WHERE [VendorId] = {vendorId}";

				foreach (var fieldsAccessor in SqlDbAccessHelper.EnumerateResultSet(connStr, querySql))
				{
					int vendorEvtTypeId = fieldsAccessor.GetFieldValue<int>(0);
					string eventTypeName = fieldsAccessor.GetFieldValue<string>(1);
					if (vendorEventTypeId == -1 || eventTypeName == "GPS Location")
					{
						vendorEventTypeId = vendorEvtTypeId;
						if (eventTypeName == "GPS Location")
						{
							break;
						}
					}
				}

			}
			catch (Exception ex)
			{
				LogHelper.Error($"GetMockGpsEventTypeId {ex}");
			}

			return vendorEventTypeId;
		}

		public static string EnsureTrustServerCertificateInConnectionString(string connStr)
		{
			if (connStr.IndexOf(TrustServerCertificateConnectionOption) < 0)
			{
				var trustServerSuffix = connStr.EndsWith(";") ? TrustServerCertificateConnectionOption : $";{TrustServerCertificateConnectionOption}";
				connStr = connStr + trustServerSuffix;
			}

			return connStr;
		}

		private static string SetConnectionTimeoutInConnectionString(string connStr, int timeoutInSeconds)
		{
			if (connStr.IndexOf(ConnectionTimeoutKey) < 0)
			{
				var connectionTimeoutPart = $"{ConnectionTimeoutKey}={timeoutInSeconds};";
				var connectionTimeoutSuffix = connStr.EndsWith(";") ? connectionTimeoutPart : $";{connectionTimeoutPart}";
				connStr = connStr + connectionTimeoutSuffix;
			}

			return connStr;
		}

		private static string ExtractTimeZoneIdFromTFTimeZoneString(string zoneString)
		{
			int zoneLen = zoneString.IndexOf(")");
			return zoneString.Substring(zoneLen + 2);
		}
	}
}
