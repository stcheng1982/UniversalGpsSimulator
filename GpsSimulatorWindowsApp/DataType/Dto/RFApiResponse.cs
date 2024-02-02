using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.DataType
{
	public class RFApiResponse<T>
	{
		public IEnumerable<T> Items { get; set; }
	}
}
