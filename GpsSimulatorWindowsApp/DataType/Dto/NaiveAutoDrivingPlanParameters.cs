using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.DataType
{
	public class NaiveAutoDrivingPlanParameters
	{
		public NaiveAutoDrivingPlanParameters()
		{
			Acceleration = 2.788888m;
			Deceleration = 2.788888m * 2;
			MaxSpeed = 15.0m;
			TurnSpeed = 1.2m;
			MaxAngleChangeInSegment = 10;
			GpsEventsIntervalInSeconds = 1; // 1 event every 1 second
		}

		/// <summary>
		/// m/s^2
		/// </summary>
		public decimal Acceleration { get; set; }

		/// <summary>
		/// m/s^2
		/// </summary>
		public decimal Deceleration { get; set; }

		/// <summary>
		/// m/s
		/// </summary>
		public decimal MaxSpeed { get; set; }

		/// <summary>
		/// m/s
		/// </summary>
		public decimal TurnSpeed { get; set; }

		/// <summary>
		/// Degree
		/// </summary>
		public int MaxAngleChangeInSegment { get; set; }

		/// <summary>
		/// Seconds
		/// </summary>
		public int GpsEventsIntervalInSeconds { get; set; }
	}
}
