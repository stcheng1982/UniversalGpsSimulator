using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class SerialPortItemVM : ObservableObject
	{
		public SerialPortItemVM(string portName)
		{
			Name = portName;
		}

		public string Name { get; private set; }
	}
}
