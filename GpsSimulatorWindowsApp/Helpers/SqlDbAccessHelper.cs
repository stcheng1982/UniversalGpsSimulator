using Microsoft.Data.SqlClient;
using GpsSimulatorWindowsApp.DataType;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.Helpers
{
	public class SqlDbAccessHelper
	{
		public static string ComposeInsertSqlForSingleNewPlusGpsEventItem(
            int vendorId,
            short dataSourceId,
            string vehicleGpsId,
            HistoryGpsEvent eventItem,
            int vendorEventTypeId,
            decimal? mockSpeedValue,
            DateTime eventStartTime,
            DateTime createdOnTime)
		{
			string headingVal = eventItem.Heading.HasValue ? eventItem.Heading.Value.ToString() : "NULL";
			string speedVal = eventItem.Speed.HasValue ? eventItem.Speed.Value.ToString() : "0.0";
			// string distanceVal = eventItem.Distance.HasValue ? eventItem.Distance.ToString() : "NULL";
			string latVal = eventItem.Latitude.HasValue ? eventItem.Latitude.Value.ToString() : "NULL";
			string lonVal = eventItem.Longitude.HasValue ? eventItem.Longitude.Value.ToString() : "NULL";

			var insertSql = $@"
INSERT INTO [dbo].[VehicleEvent]
           ([AssetId]
           ,[VendorEventTypeID]
           ,[Latitude]
           ,[Longitude]
           ,[StartTime]
           ,[EndTime]
           ,[Heading]
           ,[Speed]
           ,[Location]
           ,[CreatedOn]
           ,[DBID]
           ,[VendorId]
           )
     VALUES
           ('{vehicleGpsId}'
           ,{vendorEventTypeId}
           ,{latVal}
           ,{lonVal}
           ,'{eventStartTime}'
           ,'{eventStartTime}'
           ,{headingVal}
           ,{speedVal}
           ,'PF Windows Gps Simulator'
           ,'{createdOnTime}'
           ,{dataSourceId}
           ,{vendorId}
           )
";

			return insertSql;
		}

		public static async Task<int> ExecuteNonQueryCommandAsync(string connStr, string commandText, SqlParameter[]? parameters)
		{
			connStr = PlusDbQueryHelper.EnsureTrustServerCertificateInConnectionString(connStr);
			using (var conn = new SqlConnection(connStr))
			{
				await conn.OpenAsync();
				using (var comm = new SqlCommand(commandText, conn))
				{
                    if (parameters?.Any() == true)
                    {
                        comm.Parameters.AddRange(parameters);
                    }

					int result = await comm.ExecuteNonQueryAsync();
                    return result;
				}
			}
		}

		public static IEnumerable<SqlRecordFieldsAccessor> EnumerateResultSet(string connStr, string commText, bool caseSensitiveInFieldName = true)
		{
			connStr = PlusDbQueryHelper.EnsureTrustServerCertificateInConnectionString(connStr);

			using (var conn = new SqlConnection(connStr))
			{
				conn.Open();

				using (var comm = new SqlCommand(commText, conn))
				{
					using (var reader = comm.ExecuteReader())
					{
						var fieldAccessor = new SqlRecordFieldsAccessor(reader);
						while (reader.Read())
						{
							yield return fieldAccessor;
						}
					}
				}
			}
		}

		public static async IAsyncEnumerable<SqlRecordFieldsAccessor> EnumerateResultSetAsync(string connStr, string commText, bool caseSensitiveInFieldName = true)
		{
			connStr = PlusDbQueryHelper.EnsureTrustServerCertificateInConnectionString(connStr);

			using (var conn = new SqlConnection(connStr))
			{
				await conn.OpenAsync();

				using (var comm = new SqlCommand(commText, conn))
				{
					using (var reader = await comm.ExecuteReaderAsync())
					{
						var fieldAccessor = new SqlRecordFieldsAccessor(reader);
						while (await reader.ReadAsync())
						{
							yield return fieldAccessor;
						}
					}
				}
			}
		}
	}

	public class SqlRecordFieldsAccessor
	{
		private readonly SqlDataReader _dr;
		public SqlRecordFieldsAccessor(SqlDataReader dr)
		{
			_dr = dr;
		}

		public T GetFieldValue<T>(int i)
		{
			if (_dr.IsDBNull(i))
			{
				return default(T);
			}

			return _dr.GetFieldValue<T>(i);
		}

		public T GetFieldValue<T>(string name)
		{
			if (_dr.IsDBNull(name))
			{
				return default(T);
			}
			return _dr.GetFieldValue<T>(name);
		}

		public async Task<T> GetFieldValueAsync<T>(int i)
		{
			bool isDbNull = await _dr.IsDBNullAsync(i);
			if (isDbNull)
			{
				return default(T);
			}

			return await _dr.GetFieldValueAsync<T>(i);
		}

		public async Task<T> GetFieldValueAsync<T>(string name)
		{
			bool isDbNull = await _dr.IsDBNullAsync(name);
			if (isDbNull)
			{
				return default(T);
			}

			return await _dr.GetFieldValueAsync<T>(name);
		}

	}
}
