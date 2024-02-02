using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace GpsSimulatorWindowsApp
{
	public class GpsDataSourcesEnumConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var ParameterString = parameter as string;
			if (ParameterString == null)
				return DependencyProperty.UnsetValue;

			if (Enum.IsDefined(value.GetType(), value) == false)
				return DependencyProperty.UnsetValue;

			object paramvalue = Enum.Parse(value.GetType(), ParameterString);
			return paramvalue.Equals(value);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var ParameterString = parameter as string;
			var valueAsBool = (bool)value;

			switch (parameter)
			{
				case "Helpers":
					return valueAsBool ? GpsDataSourceType.WebEventSource : GpsDataSourceType.LocalFileEventSource;
					break;
				case "NmeaLogFile":
					return valueAsBool ? GpsDataSourceType.LocalFileEventSource : GpsDataSourceType.WebEventSource;
					break;
				default:
					throw new NotSupportedException();
			}
		}
	}
}
