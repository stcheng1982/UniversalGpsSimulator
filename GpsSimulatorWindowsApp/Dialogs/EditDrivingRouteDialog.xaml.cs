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
	/// Interaction logic for EditDrivingRouteDialog.xaml
	/// </summary>
	public partial class EditDrivingRouteDialog : Window
	{
		public EditDrivingRouteDialog()
		{
			InitializeComponent();
		}

		private async void Window_Loaded(object sender, RoutedEventArgs e)
		{
			try
			{
				var editRouteVM = (DataContext as EditDrivingRouteViewModel);
				if (editRouteVM != null)
				{
					await editRouteVM.InitializeRouteEditorWebView2Async(routeEditorWebView);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error occurred in Window initialization: {ex.Message}");
			}
		}
    }
}
