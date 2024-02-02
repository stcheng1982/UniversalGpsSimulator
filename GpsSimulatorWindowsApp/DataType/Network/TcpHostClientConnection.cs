using GpsSimulatorWindowsApp.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.DataType.Network
{
	internal class TcpHostClientConnection : IDisposable
	{
		const int DefaultBufferSize = 4096;
		private bool _disposed;

		public TcpHostClientConnection(TcpClient client)
		{
			Client = client;
			Stream = client.GetStream();
			StreamWriter = new StreamWriter(Stream, Encoding.UTF8, DefaultBufferSize) { AutoFlush = false };
		}

		public TcpClient Client { get; private set; }

		public NetworkStream Stream { get; private set; }

		public StreamWriter StreamWriter { get; private set; }

		public async Task WriteAsync(string data)
		{
			try
			{
				if (Client.Connected)
				{
					await StreamWriter.WriteAsync(data).ConfigureAwait(false);
					await StreamWriter.FlushAsync().ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error in TcpHostClientConnection.WriteAsync: {ex.Message}");
			}
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				StreamWriter.Dispose();
				Stream.Dispose();
				Client.Dispose();
				_disposed = true;
			}
			
		}
	}
}
