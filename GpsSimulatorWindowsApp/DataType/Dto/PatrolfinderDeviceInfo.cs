using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace GpsSimulatorWindowsApp.DataType
{
	[ComVisible(true)]
	public class PatrolfinderDeviceInfo
	{
		public string DeviceIdentifier { get; set; }

		public string MachineName { get; set; }

		public string OSVersion { get; set; }

		public string TimeZoneName { get; set; }
	}
}
