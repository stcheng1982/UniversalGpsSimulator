using GpsSimulatorWindowsApp.DataType.Network;
using GpsSimulatorWindowsApp.Helpers;
using GpsSimulatorWindowsApp.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GpsSimulatorWindowsApp.DataType
{
	public class SingleDeviceMockGpsEventPublisher : IDisposable
	{
		public const string AndroidMockGpsAgentAppNamespace = "com.transfinder.mobile.android.mockgpsagent";

		private bool _disposed;

		// Virtual SerialPort fields
		public bool SendToVirtualSerialPort { get; private set; }
		public string SerialPortName { get; private set; }
		public int SerialPortBaudRate { get; private set; }
		private SerialPort? _serialPort { get; set; }


		// Local TCP Host fields
		public bool EnableLocalTcpHost { get; private set; }
		public string DeviceIdForLocalTcpHost { get; private set; }
		public NmeaSentencePlaybackOptions LocalTcpHostNmeaOptions { get; private set; }
		public IPAddress LocalTcpHostIpAddress { get; private set; }
		public int LocalTcpHostPort { get; private set; }
		private TcpListener _localTcpListener = null;
		private Task _localTcpListeningTask = null;
		private CancellationTokenSource _localTcpListeningCancelTokenSource = null;
		private List<TcpHostClientConnection> _tcpHostClientConnections = new List<TcpHostClientConnection>();

		// Server UDP fields
		public bool SendToServerViaUdp { get; private set; }
		public string DeviceIdForServerUdp { get; private set; }
		public NmeaSentencePlaybackOptions ServerUdpNmeaOptions { get; private set; }
		public string ServerUdpHostIp { get; private set; }
		public int ServerUdpPort { get; private set; }
		private UdpClient? _serverUdpClient = null;

		// Android Mobile ADB & UDP fields
		public bool SendToAndroidViaAdb { get; private set; }
		public string AndroidAdbSerialNumber { get; private set; }
		public bool SendToAndroidViaUdp { get; private set; }
		public string AndroidUdpHostIp { get; private set; }
		public int AndroidUdpPort { get; private set; }

		// iOS Mobile fields
		public bool SendToIOSDevice { get; private set; }
		public string IOSDeviceUdid { get; private set; }


		public SingleDeviceMockGpsEventPublisher(
			GpsSimulationProfile gpsSimulationProfile
			)
		{
			// Virtual SerialPort
			SendToVirtualSerialPort = gpsSimulationProfile.SendToVirtualSerialPort ?? false;
			SerialPortName = gpsSimulationProfile.SerialPortName;
			SerialPortBaudRate = gpsSimulationProfile.SerialPortBaudRate;

			// Local TCP Host
			EnableLocalTcpHost = gpsSimulationProfile.EnableLocalTcpHost ?? false;
			DeviceIdForLocalTcpHost = gpsSimulationProfile.DeviceIdForLocalTcpHost;
			LocalTcpHostNmeaOptions = gpsSimulationProfile.LocalTcpHostNmeaOptions ?? NmeaSentencePlaybackOptions.Default;
			if (EnableLocalTcpHost)
			{
				string localTcpHostIpValue = null;
				IPAddress hostIpAdress = null;
				int localTcpHostPort = 0;
				var hostInfoFields = gpsSimulationProfile.LocalTcpHostInfo?.Split(':');
				if (hostInfoFields?.Length == 2)
				{
					localTcpHostIpValue = hostInfoFields[0];
					if (IPAddress.TryParse(localTcpHostIpValue, out IPAddress ipAddress))
					{
						LocalTcpHostIpAddress = ipAddress;
					}

					if (int.TryParse(hostInfoFields[1], out int portValue))
					{
						LocalTcpHostPort = portValue;
					}
				}
			}

			// Server UDP
			SendToServerViaUdp = gpsSimulationProfile.SendToServerViaUdp ?? false;
			DeviceIdForServerUdp = gpsSimulationProfile.DeviceIdForServerUdp;
			ServerUdpNmeaOptions = gpsSimulationProfile.ServerUdpNmeaOptions ?? NmeaSentencePlaybackOptions.Default;
			ServerUdpHostIp = null;
			ServerUdpPort = 0;
			if (SendToServerViaUdp)
			{
				var hostInfoFields = gpsSimulationProfile.ServerUdpHostInfo?.Split(':');
				if (hostInfoFields?.Length == 2)
				{
					ServerUdpHostIp = hostInfoFields[0];
					if (int.TryParse(hostInfoFields[1], out int portValue))
					{
						ServerUdpPort = portValue;
					}
				}
			}

			// Android Mobile ADB & UDP
			SendToAndroidViaAdb = gpsSimulationProfile.SendToAndroidViaAdb ?? false;
			AndroidAdbSerialNumber = gpsSimulationProfile.AndroidAdbSerialNumber;
			SendToAndroidViaUdp = gpsSimulationProfile.SendToAndroidViaUdp ?? false;
			AndroidUdpHostIp = null;
			AndroidUdpPort = 0;
			if (SendToAndroidViaUdp)
			{
				var hostInfoFields = gpsSimulationProfile.AndroidUdpHostInfo?.Split(':');
				if (hostInfoFields?.Length == 2)
				{
					AndroidUdpHostIp = hostInfoFields[0];
					if (int.TryParse(hostInfoFields[1], out int portValue))
					{
						AndroidUdpPort = portValue;
					}
				}
			}

			// iOS Mobile
			SendToIOSDevice = gpsSimulationProfile.SendToIOSDevice ?? false;
			IOSDeviceUdid = gpsSimulationProfile.IOSDeviceUdid;
		}

		public void InitializeResourcesForAllMockTargets()
		{
			if (SendToVirtualSerialPort)
			{
				_serialPort = TryOpeningTargetSerialPort(SerialPortName, SerialPortBaudRate);
				if (_serialPort == null)
				{
					MessageBox.Show($"Failed to connect SerialPort: {SerialPortName}");
				}
			}

			if (EnableLocalTcpHost)
			{
				StartLocalTcpHost();
			}

		}

		public void CleanupResourcesForAllMockTargets()
		{
			// Clean up Virtual Serial Port resource before exit playback
			TryClosingTargetSerialPort();

			// Clean up Local TCP Host resource before exit playback
			if (EnableLocalTcpHost)
			{
				StopLocalTcpHostAsync().ConfigureAwait(false);
			}

			// Stop mobile mock location service
			if (SendToAndroidViaAdb || SendToAndroidViaUdp)
			{
				StopAndroidMockLocationService();
			}

			// TBD: Stop mobile mock loation service for iOS device
			if (SendToIOSDevice)
			{
				StopIOSLibmobileLocationService();
			}
		}

		public async Task PublishNewMockGpsEventAsync(HistoryGpsEvent newGpsEvent, DateTime sentOn, decimal? customSpeed)
		{
			try
			{
				if (newGpsEvent == null)
				{
					return;
				}

				if (SendToVirtualSerialPort && _serialPort != null)
				{
					string rmcSentence;
					if (customSpeed == null)
					{
						rmcSentence = NmeaDataHelper.ConvertHistoryGpsEventToNmeaRmcSentence(newGpsEvent, sentOn);
					}
					else
					{
						rmcSentence = NmeaDataHelper.ConvertHistoryGpsEventToNmeaRmcSentence(newGpsEvent, sentOn, customSpeed.Value);
					}

					SendMockGpsLocationToSerialPortAsync(_serialPort, rmcSentence).ConfigureAwait(false);
				}

				if (EnableLocalTcpHost)
				{
					SafeWriteToLocalTcpHostClients(DeviceIdForLocalTcpHost, LocalTcpHostNmeaOptions, newGpsEvent, sentOn, customSpeed);
				}

				if (SendToServerViaUdp)
				{
					SendMockGpsLocationToServerByUdp(ServerUdpHostIp, ServerUdpPort, DeviceIdForServerUdp, ServerUdpNmeaOptions, newGpsEvent, sentOn, customSpeed);
				}

				if (SendToAndroidViaAdb)
				{
					SendMockGpsLocationToAndroidByAdbShell(AndroidAdbSerialNumber, newGpsEvent, customSpeed);
				}

				if (SendToAndroidViaUdp)
				{
					SendMockGpsLocationToAndroidByUdp(AndroidUdpHostIp, AndroidUdpPort, newGpsEvent, customSpeed);
				}

				if (SendToIOSDevice)
				{
					SendMockGpsLocationToIOSByLibimobile(IOSDeviceUdid, newGpsEvent, customSpeed);
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex.ToString());
			}
		}

		private SerialPort? TryOpeningTargetSerialPort(string? portName, int baudRate)
		{
			try
			{
				var matchedPortName = SerialPort.GetPortNames().FirstOrDefault(pn => pn.Equals(portName, StringComparison.OrdinalIgnoreCase));
				if (matchedPortName == null)
				{
					MessageBox.Show($"Serial Port '{portName}' not found.");
					return null;
				}

				var dataBits = 8;
				var parity = Parity.None;
				var encoding = Encoding.ASCII;
				var port = new SerialPort(matchedPortName);

				port.Encoding = encoding;
				port.BaudRate = baudRate;
				port.DataBits = dataBits;
				port.Parity = parity;
				// port.StopBits = StopBits.One;
				port.WriteTimeout = 500;
				port.WriteBufferSize = 2048;

				port.Open();
				if (!port.IsOpen)
				{
					port.Dispose();
					return null;
				}

				return port;
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
				return null;
			}
		}

		private void TryClosingTargetSerialPort()
		{
			try
			{
				// Clean up Virtual Serial Port resource before exit playback
				if (SendToVirtualSerialPort && _serialPort != null)
				{
					_serialPort.Close();
					_serialPort = null;
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex);
				MessageBox.Show(ex.Message);
			}
		}

		private void StartLocalTcpHost()
		{
			try
			{
				_localTcpListeningCancelTokenSource = new CancellationTokenSource();
				var ipEndpoint = new IPEndPoint(LocalTcpHostIpAddress, LocalTcpHostPort);
				_localTcpListener = new TcpListener(ipEndpoint);
				_localTcpListener.Start();

				_localTcpListeningTask = Task.Run(async () =>
				{
					while (!_localTcpListeningCancelTokenSource.IsCancellationRequested)
					{
						try
						{
							var client = await _localTcpListener.AcceptTcpClientAsync(_localTcpListeningCancelTokenSource.Token);
							if (client != null)
							{
								var clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
								LogHelper.Info($"Local TCP Host accepted client from {clientIp}");
								var clientConnection = new TcpHostClientConnection(client);
								_tcpHostClientConnections.Add(clientConnection);
							}
						}
						catch (Exception ex)
						{
							LogHelper.Error(ex);
						}
					}
				});

			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to init Local TCP Host: {ex.Message}");
				_localTcpListener = null;
				_localTcpListeningCancelTokenSource?.Dispose();
				_localTcpListeningCancelTokenSource = null;
				_localTcpListeningTask = null;
			}
		}

		private async Task StopLocalTcpHostAsync()
		{
			try
			{
				_localTcpListeningCancelTokenSource?.Cancel();
				await Task.Delay(2000);

				if (_tcpHostClientConnections?.Any() == true)
				{
					var clientConnections = _tcpHostClientConnections.ToList();
					foreach (var clientConnection in clientConnections)
					{
						try
						{
							clientConnection.Dispose();
						}
						catch (Exception ex)
						{
							LogHelper.Error($"Error occurred when disposing clientConnection of LocalTcpHost. {ex}");
						}
					}

					_tcpHostClientConnections.Clear();
				}

				_localTcpListener?.Stop();
				_localTcpListener = null;
				_localTcpListeningCancelTokenSource?.Dispose();
				_localTcpListeningCancelTokenSource = null;
				_localTcpListeningTask = null;
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error occurred when stopping local TCP Host. {ex}");
			}
		}

		private void StopAndroidMockLocationService()
		{
			try
			{
				var cmdStr = "cmd";
				var cmdArgs = $"/c adb shell am stopservice {AndroidMockGpsAgentAppNamespace}/.LocationService";
				RunExternalCommandWithoutOutput(cmdStr, cmdArgs);

				cmdStr = "cmd";
				cmdArgs = $"/c adb kill-server";
				RunExternalCommandWithoutOutput(cmdStr, cmdArgs);
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Unexpected Error in StopAndroidMockLocationService: {ex.Message}");
			}
		}

		private void StopIOSLibmobileLocationService()
		{
			try
			{
				IOSAutomationHelper.TryResettingMockLocationService();
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Unexpected Error in StopIOSLibmobileLocationService: {ex}");
			}
		}

		private async Task SafeWriteToLocalTcpHostClients(
			string deviceId,
			NmeaSentencePlaybackOptions nmeaOptions,
			HistoryGpsEvent gpsEvent,
			DateTime sentOn,
			decimal? mockSpeedValue)
		{
			if (_tcpHostClientConnections?.Any() == true)
			{
				// Compose series of NMEA sentences based on the GPS event and NMEA sentence playback options

				// $GNGNS,090145.00,4248.894583,N,07356.197462,W,AANN,16,0.7,75.1,-36.0,,,V * 2B
				// $GPGGA,090145.00,4248.894583,N,07356.197462,W,1,09,0.7,75.1,M,-36.0,M,,*6B
				// $GPRMC,090145.00,A,4248.894583,N,07356.197462,W,0.0,349.9,081123,11.1,W,A,V * 70
				// $GPVTG,349.9,T,1.0,M,0.0,N,0.0,K,A * 25
				// $PCPTI,IBR900 - 6de,90145,90145 * 08

				var dataBuf = new StringBuilder();
				try
				{
					if (nmeaOptions.GNGNSEnabled)
					{
						var gnsSentence = NmeaDataHelper.ComposeNmeaGnsSentence(gpsEvent, sentOn);
						dataBuf.Append(gnsSentence);
					}

					if (nmeaOptions.GPGGAEnabled)
					{
						var ggaSentence = NmeaDataHelper.ComposeNmeaGgaSentence(gpsEvent, sentOn);
						dataBuf.Append(ggaSentence);
					}

					if (nmeaOptions.GPGLLEnabled)
					{
						var gllSentence = NmeaDataHelper.ComposeNmeaGllSentence(gpsEvent, sentOn);
						dataBuf.Append(gllSentence);
					}

					if (nmeaOptions.GPRMCEnabled)
					{
						string rmcSentence;
						if (!mockSpeedValue.HasValue)
						{
							// nextGpsEvent.Speed.HasValue && nextGpsEvent.Speed.Value > 0.5m
							rmcSentence = NmeaDataHelper.ConvertHistoryGpsEventToNmeaRmcSentence(gpsEvent, sentOn);
						}
						else
						{
							rmcSentence = NmeaDataHelper.ConvertHistoryGpsEventToNmeaRmcSentence(gpsEvent, sentOn, mockSpeedValue.Value);
						}
						dataBuf.Append(rmcSentence);
					}

					if (nmeaOptions.GPVTGEnabled)
					{
						var vtgSentence = NmeaDataHelper.ComposeNmeaVtgSentence(gpsEvent, mockSpeedValue);
						dataBuf.Append(vtgSentence);
					}

					var deviceIdSentence = NmeaDataHelper.ComposeDeviceIdNmeaSentence(nmeaOptions.DeviceProfileName, deviceId);
					if (!string.IsNullOrEmpty(deviceIdSentence))
					{
						dataBuf.Append(deviceIdSentence);
					}
				}
				catch (Exception ex)
				{
					LogHelper.Error(ex);
				}

				if (dataBuf.Length > 0)
				{
					var clientConnections = _tcpHostClientConnections.ToList();
					foreach (var clientConnection in clientConnections)
					{
						try
						{
							if (!clientConnection.Client.Connected)
							{
								clientConnection.Dispose();
								_tcpHostClientConnections.Remove(clientConnection);
								continue;
							}

							clientConnection.WriteAsync(dataBuf.ToString());
						}
						catch (Exception ex)
						{
							LogHelper.Error(ex);
						}
					}
				}
			}
		}

		static void SafeWriteToSerialPort(SerialPort? port, string data)
		{
			try
			{
				//var bytes = Encoding.ASCII.GetBytes(data);
				//port.Write(bytes, 0, bytes.Length);
				port?.Write(data);
			}
			catch (TimeoutException)
			{
				// Ignore
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Unexpected Error in SafeWriteToSerialPort: {ex.Message}");
			}
		}

		static async Task SendMockGpsLocationToSerialPortAsync(SerialPort port, string rmcSentence)
		{
			await Task.Run(() =>
			{
				SafeWriteToSerialPort(port, NmeaDataHelper.NmeaSentencesBeforeRmcSentence);
				SafeWriteToSerialPort(port, rmcSentence);
				SafeWriteToSerialPort(port, NmeaDataHelper.NmeaSentencesAfterRmcSentences);
			});
		}

		static void SendMockGpsLocationToAndroidByAdbShell(string deviceSerialNumber, HistoryGpsEvent gpsEvent, decimal? mockSpeedValue)
		{
			try
			{
				var cmdStr = "cmd";
				var headingValue = gpsEvent.Heading ?? 0;
				var kmphSpeedValue = mockSpeedValue.HasValue ? mockSpeedValue.Value : gpsEvent.Speed ?? 0;
				var mpsValue = (kmphSpeedValue * 1000 / 3600).ToString("f2");

				var deviceSerialArg = string.IsNullOrEmpty(deviceSerialNumber) ? string.Empty : $"-s {deviceSerialNumber}";
				var cmdArgs = $"/c adb {deviceSerialArg} shell am start-foreground-service --user 0 -n {AndroidMockGpsAgentAppNamespace}/.LocationService --es longitude {gpsEvent.Longitude ?? 0} --es latitude {gpsEvent.Latitude ?? 0}  --es altitude 10 --es speed {mpsValue} --es bearing {headingValue}";
				RunExternalCommandWithoutOutput(cmdStr, cmdArgs);
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Unexpected Error in SendMockGpsLocationToAndroidMockLocationService: {ex.Message}");
			}
		}

		static void SendMockGpsLocationToServerByUdp(
			string udpHostIp,
			int udpPort,
			string deviceId,
			NmeaSentencePlaybackOptions nmeaOptions,
			HistoryGpsEvent gpsEvent,
			DateTime sentOn,
			decimal? mockSpeedValue)
		{
			// Compose series of NMEA sentences based on the GPS event and NMEA sentence playback options

			// $GNGNS,090145.00,4248.894583,N,07356.197462,W,AANN,16,0.7,75.1,-36.0,,,V * 2B
			// $GPGGA,090145.00,4248.894583,N,07356.197462,W,1,09,0.7,75.1,M,-36.0,M,,*6B
			// $GPRMC,090145.00,A,4248.894583,N,07356.197462,W,0.0,349.9,081123,11.1,W,A,V * 70
			// $GPVTG,349.9,T,1.0,M,0.0,N,0.0,K,A * 25
			// $PCPTI,IBR900 - 6de,90145,90145 * 08

			var dataBuf = new StringBuilder();

			if (nmeaOptions.GNGNSEnabled)
			{
				var gnsSentence = NmeaDataHelper.ComposeNmeaGnsSentence(gpsEvent, sentOn);
				dataBuf.Append(gnsSentence);
			}

			if (nmeaOptions.GPGGAEnabled)
			{
				var ggaSentence = NmeaDataHelper.ComposeNmeaGgaSentence(gpsEvent, sentOn);
				dataBuf.Append(ggaSentence);
			}

			if (nmeaOptions.GPGLLEnabled)
			{
				var gllSentence = NmeaDataHelper.ComposeNmeaGllSentence(gpsEvent, sentOn);
				dataBuf.Append(gllSentence);
			}

			if (nmeaOptions.GPRMCEnabled)
			{
				string rmcSentence;
				if (!mockSpeedValue.HasValue)
				{
					// nextGpsEvent.Speed.HasValue && nextGpsEvent.Speed.Value > 0.5m
					rmcSentence = NmeaDataHelper.ConvertHistoryGpsEventToNmeaRmcSentence(gpsEvent, sentOn);
				}
				else
				{
					rmcSentence = NmeaDataHelper.ConvertHistoryGpsEventToNmeaRmcSentence(gpsEvent, sentOn, mockSpeedValue.Value);
				}
				dataBuf.Append(rmcSentence);
			}

			if (nmeaOptions.GPVTGEnabled)
			{
				var vtgSentence = NmeaDataHelper.ComposeNmeaVtgSentence(gpsEvent, mockSpeedValue);
				dataBuf.Append(vtgSentence);
			}

			var deviceIdSentence = NmeaDataHelper.ComposeDeviceIdNmeaSentence(nmeaOptions.DeviceProfileName, deviceId);
			if (!string.IsNullOrEmpty(deviceIdSentence))
			{
				dataBuf.Append(deviceIdSentence);
			}

			SendUdpDatagram(udpHostIp, udpPort, dataBuf.ToString());
		}

		static void SendMockGpsLocationToAndroidByUdp(string udpHostIp, int udpPort, HistoryGpsEvent gpsEvent, decimal? mockSpeedValue)
		{
			if (!System.Net.IPAddress.TryParse(udpHostIp, out _))
			{
				return;
			}

			if (udpPort <= 0)
			{
				return;
			}

			try
			{
				var headingValue = gpsEvent.Heading ?? 0;
				var kmphSpeedValue = mockSpeedValue.HasValue ? mockSpeedValue.Value : gpsEvent.Speed ?? 0;
				var mpsValue = (kmphSpeedValue * 1000 / 3600).ToString("f2");

				var msgFieldsDict = new Dictionary<string, string>();
				msgFieldsDict.Add("lng", $"{gpsEvent.Longitude ?? 0}");
				msgFieldsDict.Add("lat", $"{gpsEvent.Latitude ?? 0}");
				msgFieldsDict.Add("alt", $"10");
				msgFieldsDict.Add("speed", $"{mpsValue}");
				msgFieldsDict.Add("bearing", $"{headingValue}");

				var udpMsg = JsonSerializer.Serialize(msgFieldsDict);
				SendUdpDatagram(udpHostIp, udpPort, udpMsg);

			}
			catch (Exception ex)
			{
				LogHelper.Error($"Unexpected Error in SendMockGpsLocationToAndroidByUdp: {ex.Message}");
			}
		}


		private void SendMockGpsLocationToIOSByLibimobile(string deviceUdid, HistoryGpsEvent gpsEvent, decimal? mockSpeedValue)
		{
			try
			{
				IOSAutomationHelper.TrySettingMockLocationToIOSDevice(deviceUdid, gpsEvent.Longitude ?? 0, gpsEvent.Latitude ?? 0);
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Unexpected Error in SendMockGpsLocationToIOSByLibimobile: {ex.Message}");
			}
		}

		/// <summary>
		/// Run external command without redirecting and obtaining the STD output & error
		/// </summary>
		/// <param name="cmd"></param>
		/// <param name="cmdArgs"></param>
		/// <param name="workingDir"></param>
		/// <returns></returns>
		private static void RunExternalCommandWithoutOutput(string cmd, string cmdArgs, string workingDir = null)
		{
			var startInfo = new ProcessStartInfo(cmd, cmdArgs)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				LoadUserProfile = false,
				RedirectStandardOutput = false,
				RedirectStandardError = false,
			};

			if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
			{
				startInfo.WorkingDirectory = workingDir;
			}

			var p = Process.Start(startInfo);
			//await p.WaitForExitAsync();
		}

		private static void SendUdpDatagram(string udpAddress, int udpPort, string data)
		{
			using (var udpClient = new UdpClient())
			{
				var bytes = Encoding.ASCII.GetBytes(data);
				udpClient.Send(bytes, bytes.Length, udpAddress, udpPort);
			}
		}

		#region -- IDisposable Support --

		public void Dispose()
		{
			if (_disposed) return;

			CleanupResourcesForAllMockTargets();

			_disposed = true;
		}

		#endregion
	}
}
