using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class NotifyIconViewModel : ObservableObject
	{
		///// <summary>
		///// Shows a window, if none is already open.
		///// </summary>
		//public ICommand ShowWindowCommand
		//{
		//	get
		//	{
		//		return new RelayCommand(() => Application.Current.MainWindow == null ||
		//				!Application.Current.MainWindow.IsVisible,
		//				() =>
		//			{
		//				Application.Current.MainWindow = new MainWindow();
		//				Application.Current.MainWindow.Show();
		//			}
		//		);
		//	}
		//}

		///// <summary>
		///// Hides the main window. This command is only enabled if a window is open.
		///// </summary>
		//public ICommand HideWindowCommand
		//{
		//	get
		//	{
		//		return new RelayCommand
		//		{
		//			CommandAction = () => Application.Current.MainWindow.Close(),

		//			CanExecuteFunc = () => Application.Current.MainWindow != null &&
		//				Application.Current.MainWindow.IsVisible
		//		};
		//	}
		//}


		///// <summary>
		///// Shuts down the application.
		///// </summary>
		//public ICommand ExitApplicationCommand
		//{
		//	get
		//	{
		//		return new RelayCommand { CommandAction = () => Application.Current.Shutdown() };
		//	}
		//}
	}
}
