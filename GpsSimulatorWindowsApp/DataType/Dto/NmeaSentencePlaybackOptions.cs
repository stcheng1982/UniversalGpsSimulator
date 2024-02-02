using GpsSimulatorWindowsApp.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.DataType
{
	public class NmeaSentencePlaybackOptions
	{
		public static NmeaSentencePlaybackOptions Default
		{
			get
			{
				return new NmeaSentencePlaybackOptions
				{
					GNGNSEnabled = true,
					GPGLLEnabled = true,
					GPGGAEnabled = true,
					GPRMCEnabled = true,
					GPVTGEnabled = true,
					DeviceProfileName = NmeaDataHelper.CradleDeviceProfileName,
				};
			}
		}

		public NmeaSentencePlaybackOptions() { }

		public bool GNGNSEnabled { get; set; }

		public bool GPGLLEnabled { get; set; }

		public bool GPGGAEnabled { get; set; }

		public bool GPRMCEnabled { get; set; }

		public bool GPVTGEnabled { get; set; }

		public string? DeviceProfileName { get; set; }
	}
}
