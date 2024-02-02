using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class BaudRateItemVM : ObservableObject
	{
		public int Value { get; private set; }

		public BaudRateItemVM(int rate)
		{ 
			Value = rate;
		}
	}
}
