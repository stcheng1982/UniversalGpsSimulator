using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class PlusVehicleInputViewModel : MultiDeviceItemInputViewModel
	{
		private string _vehicleGpsId;

		public string VehicleGpsId
		{
			get => _vehicleGpsId;
			set
			{
				SetProperty(ref _vehicleGpsId, value, nameof(VehicleGpsId));
			}
		}

		public PlusVehicleInputViewModel(GpsDataSourceViewModel parentVM) : base(parentVM)
		{
		}
	}
}
