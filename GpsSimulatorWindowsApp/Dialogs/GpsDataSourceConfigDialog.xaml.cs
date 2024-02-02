using GpsSimulatorWindowsApp.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GpsSimulatorWindowsApp.Dialogs
{
	/// <summary>
	/// Interaction logic for GpsDataSourceConfigDialog.xaml
	/// </summary>
	public partial class GpsDataSourceConfigDialog : Window
	{
		private GpsDataSourceViewModel _viewModel;
		public GpsDataSourceConfigDialog(GpsDataSourceViewModel viewModel)
		{
			_viewModel = viewModel;
			InitializeComponent();
			DataContext = _viewModel;
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
 
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			DataContext = null;
			_viewModel = null;
		}
	}
}
