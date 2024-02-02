using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.DataType
{
	public class VirtualDrivingProfile
	{
		public string Name { get; set; }

		public decimal Acceleration { get; set; }

		public decimal Deceleration { get; set; }

		public decimal MaxSpeed { get; set; }

		public decimal DragCoefficient { get; set; }

		public decimal Mass { get; set; }

		public decimal DeltaAnglePerSecond { get; set; }
	}

	public class VirtualDrivingRoute
	{
		public string Name { get; set; } = string.Empty;

		public string RouteJsonDataFilePath { get; set; } = string.Empty;
	}

	public class VirtualDrivingStartupSettings
	{
		public GpsSimulationProfile GpsSimulationProfile { get; set; }

		public int FramePerSecond { get; set; }

		public VirtualDrivingControlMethod ControlMethod { get; set; }

		public VirtualDrivingProfile DrivingProfile { get; set; }

		public bool SendDrivingGpsEventToDeviceMockTarget { get; set; }

		public bool AutoSaveGpsEventsAfterDrivingComplete { get; set; }

		public string? AutoSaveGpsEventsDirectoryPath { get; set; }

		public bool UsePredefinedRoute { get; set; }

		public VirtualDrivingRouteConductType RouteConductType { get; set; }

		public VirtualDrivingRoute? SelectedRoute { get; set; }
	}
}
