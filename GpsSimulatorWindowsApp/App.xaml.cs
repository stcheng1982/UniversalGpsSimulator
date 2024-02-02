using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using GpsSimulatorWindowsApp.ViewModel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Reflection;
using GpsSimulatorWindowsApp.Logging;

namespace GpsSimulatorWindowsApp
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		static Mutex mutex = new Mutex(true, "F6A988E5-45F6-4CCB-BFE4-D2F4B1365A16");

		public static SplashScreen SplashScreen { get; private set; }

		public App()
		{
			if (!mutex.WaitOne(TimeSpan.Zero, true))
			{
				MessageBox.Show("Application is already running");
				Shutdown();
				return;
			}

			SplashScreen = new SplashScreen("SplashscreenImage.png");
			SplashScreen.Show(false, true);

			Task.Delay(30 * 1000).ContinueWith(t =>
			{
				TryDismissingSplashScreen();
			});

			Logging.SerilogSetup.SetupSerilog();

			Services = ConfigureServices();

			SetupUnhandledExceptionHandling();
		}

		public new static App Current => (App)Application.Current;

		public IServiceProvider Services { get; }

		public void TryDismissingSplashScreen()
		{
			if (SplashScreen != null)
			{
				SplashScreen.Close(TimeSpan.FromSeconds(1));
				SplashScreen = null;
			}
		}

		protected override void OnExit(ExitEventArgs e)
		{
			mutex.ReleaseMutex();
			base.OnExit(e);
		}

		private static IServiceProvider ConfigureServices()
		{
			var services = new ServiceCollection();

			services.AddHttpClient(); // Adds IHttpClientFactory based httpclient

			services.AddSingleton<MainWindowViewModel>();

			return services.BuildServiceProvider();
		}
		public MainWindowViewModel MainWindowViewModel => Services.GetService<MainWindowViewModel>();

		private void SetupUnhandledExceptionHandling()
		{
			AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
				ShowUnhandledException(args.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException", false);

			TaskScheduler.UnobservedTaskException += (sender, args) =>
				ShowUnhandledException(args.Exception, "TaskScheduler.UnobservedTaskException", false);

			// Catch exceptions from a single specific UI dispatcher thread.
			Dispatcher.UnhandledException += (sender, args) =>
			{
				// If we are debugging, let Visual Studio handle the exception and take us to the code that threw it.
				if (!Debugger.IsAttached)
				{
					args.Handled = true;
					ShowUnhandledException(args.Exception, "Dispatcher.UnhandledException", true);
				}
			};
		}

		void ShowUnhandledException(Exception e, string unhandledExceptionType, bool promptUserForShutdown)
		{
			var messageBoxTitle = $"Unexpected Error Occurred: {unhandledExceptionType}";
			var messageBoxMessage = $"The following exception occurred:\n\n{e}";
			var messageBoxButtons = MessageBoxButton.OK;

			LogHelper.Error(messageBoxMessage);

			if (promptUserForShutdown)
			{
				messageBoxMessage += "\n\nNormally the app would die now. Should we let it die?";
				messageBoxButtons = MessageBoxButton.YesNo;
			}

			if (MessageBox.Show(messageBoxMessage, messageBoxTitle, messageBoxButtons) == MessageBoxResult.Yes)
			{
				Application.Current.Shutdown();
			}
		}
	}
}
