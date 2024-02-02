using GpsSimulatorWindowsApp.ViewModel;
using System;
using System.Drawing;
using System.Windows;
using winform = System.Windows.Forms;

namespace GpsSimulatorWindowsApp
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private winform.NotifyIcon _notifyIcon;

		public MainWindow()
		{
			InitializeComponent();

			InitializeNotifyIcon();

			DataContext = App.Current.MainWindowViewModel;
		}

		protected override void OnStateChanged(EventArgs e)
		{
			if (WindowState == WindowState.Minimized)
			{
				this.Hide();
			}
			else 
			{
			}

			base.OnStateChanged(e);
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			if (DataContext != null)
			{
				(DataContext as MainWindowViewModel)?.Dispose();
			}
		}

		void InitializeNotifyIcon()
		{
			_notifyIcon = new winform.NotifyIcon();
			_notifyIcon.Icon = new System.Drawing.Icon("TrayIcon.ico");
			_notifyIcon.Visible = true;

			// _notifyIcon.BalloonTipTitle = "Information";
			_notifyIcon.Text = "TF Universal GPS Simulator";
			_notifyIcon.BalloonTipText = "TF Universal GPS Simulator is still running";
			_notifyIcon.BalloonTipIcon = winform.ToolTipIcon.Info;
			
			_notifyIcon.DoubleClick += (sender, args) =>
				{
					this.Show();
					this.WindowState = WindowState.Normal;
				};

			var ctxMenu = new winform.ContextMenuStrip();
			ctxMenu.Items.Add("Show", null, (sender, args) =>
			{
				this.Show();
				this.WindowState = WindowState.Normal;
			});

			ctxMenu.Items.Add("Exit", null, (sender, args) =>
			{
				this.Close();
			});


			_notifyIcon.ContextMenuStrip = ctxMenu;
			
		}

		private async void Window_Loaded(object sender, RoutedEventArgs e)
		{
			try
			{
				var mainVM = (DataContext as MainWindowViewModel);
				if (mainVM != null)
				{
					await mainVM.SetMainWindowInstanceOnLoadedAsync(this);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error occurred in Window initialization: {ex.Message}");
			}
		}


		private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
		{
			this.WindowState = WindowState.Minimized;
		}

		private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e)
		{
			if (this.WindowState == WindowState.Maximized)
			{
				this.WindowState = WindowState.Normal;
			}
			else
			{
				this.WindowState = WindowState.Maximized;
			}
		}

		private void OnCloseButtonClick(object sender, RoutedEventArgs e)
		{
			this.Hide();
			_notifyIcon.ShowBalloonTip(5 * 1000);
		}

		private void RefreshMaximizeRestoreButton()
		{
			if (this.WindowState == WindowState.Maximized)
			{
				this.maximizeButton.Visibility = Visibility.Collapsed;
				this.restoreButton.Visibility = Visibility.Visible;
			}
			else
			{
				this.maximizeButton.Visibility = Visibility.Visible;
				this.restoreButton.Visibility = Visibility.Collapsed;
			}
		}
	}
}
