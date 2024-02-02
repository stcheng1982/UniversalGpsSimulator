using NmeaParser.Messages;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Helpers;
using System;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace GpsSimulatorWindowsApp.UnitTest
{
	public class NmeaSentenceParsingTest
	{
		const string RmcMessageType = "GPRMC";

		[Fact]
		public void SimpleRmcSentenceParseTest()
		{
			var sentence1 = "$GPRMC,170920.0,A,4242.746707,N,07346.003608,W,0.0,0.0,240323,11.2,W,A*30\r\n";
			var msg = Rmc.Parse(sentence1);
			Assert.NotNull(msg);
			Assert.Equal(RmcMessageType, msg.MessageType);
			var rmcMsg = msg as Rmc;
			Assert.NotNull(rmcMsg);
			Assert.True(rmcMsg.Active);
			Assert.True(rmcMsg.Latitude > 0);
			Assert.True(rmcMsg.Longitude < 0);
			Assert.Equal<double>(0, rmcMsg.Speed);
			Assert.Equal<double>(0, rmcMsg.Course);
			Assert.True(rmcMsg.FixTime.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") == "2023-03-24 17:09:20");

			sentence1 = "$GPRMC,170920.0,A,4242.746707,N,07346.003608,W,0.0,0.0,240323,11.2,W,A*30";
			msg = Rmc.Parse(sentence1);
			Assert.NotNull(msg);
			Assert.Equal(RmcMessageType, msg.MessageType);
			rmcMsg = msg as Rmc;
			Assert.NotNull(rmcMsg);
			Assert.True(rmcMsg.Active);
			Assert.True(rmcMsg.Latitude > 0);
			Assert.True(rmcMsg.Longitude < 0);
			Assert.Equal<double>(0, rmcMsg.Speed);
			Assert.Equal<double>(0, rmcMsg.Course);
			Assert.True(rmcMsg.FixTime.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") == "2023-03-24 17:09:20");

			var sentence2 = "$GPRMC,,V,,,,,,,,,,N*53\r\n";
			msg = Rmc.Parse(sentence2);
			Assert.NotNull(msg);
			Assert.Equal(RmcMessageType, msg.MessageType);
			rmcMsg = msg as Rmc;
			Assert.NotNull(rmcMsg);
			Assert.False(rmcMsg.Active);
			Assert.True(rmcMsg.FixTime.UtcDateTime == DateTime.MinValue);

			//Assert.False(rmcMsg.Latitude == 0);
			//Assert.True(rmcMsg.Longitude ==0);
			//Assert.Equal<double>(0, rmcMsg.Speed);
			//Assert.Equal<double>(0, rmcMsg.Course);
		}

		[Fact]
		public void SimpleHistoryGpsEventParseFromRmcSentenceTest()
		{
			HistoryGpsEvent gpsEvent;
			var sentence1 = "$GPRMC,170920.0,A,4242.746707,N,07346.003608,W,0.0,0.0,240323,11.2,W,A*30\r\n";
			Assert.True(NmeaDataHelper.TryParseNmeaRmcSentenceAsHistoryGpsEvent(sentence1, out gpsEvent));
			Assert.NotNull(gpsEvent);
			Assert.True(gpsEvent.Longitude.HasValue);
			Assert.True(gpsEvent.Latitude.HasValue);
			Assert.True(gpsEvent.Speed.HasValue);
			Assert.True(gpsEvent.Heading.HasValue);
			var fixTime = DateTime.Parse("2023-03-24 17:09:20");
			DateTime.SpecifyKind(fixTime, DateTimeKind.Utc);
			Assert.Equal("2023-03-24 17:09:20", gpsEvent.StartTimeValue);
			Assert.Equal<DateTime>(fixTime, gpsEvent.StartTime);
			Assert.True(DateTimeKind.Utc == gpsEvent.StartTime.Kind);


			var sentence2 = "$GPRMC,,V,,,,,,,,,,N*53\r\n";
			Assert.True(NmeaDataHelper.TryParseNmeaRmcSentenceAsHistoryGpsEvent(sentence2, out gpsEvent));
			Assert.NotNull(gpsEvent);
			Assert.False(gpsEvent.Longitude.HasValue);
			Assert.False(gpsEvent.Latitude.HasValue);
			Assert.False(gpsEvent.Speed.HasValue);
			Assert.False(gpsEvent.Heading.HasValue);

			Assert.Equal<DateTime>(DateTime.MinValue, gpsEvent.StartTime);
			Assert.True(DateTimeKind.Utc == gpsEvent.StartTime.Kind);
		}
	}
}