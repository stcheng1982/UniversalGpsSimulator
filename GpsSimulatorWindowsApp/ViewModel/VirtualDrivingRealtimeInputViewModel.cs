using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GpsSimulatorComponentLibrary.GameEngine;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Dialogs;
using GpsSimulatorWindowsApp.Helpers;
using GpsSimulatorWindowsApp.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class VirtualDrivingRealtimeInputViewModel : ObservableObject
	{

		private CancellationTokenSource _drivingCancelTokenSource = null;
		private bool _isDriving = false;
		private Task _drivingKeyboardInputTask = null;
		private Task _drivingJoystickInputTask = null;
		private SingleDeviceMockGpsEventPublisher _mockGpsEventPublisher = null;
		private List<Task> _virtualDrivingInputTasks = new List<Task>();

		private VirtualDrivingStartupSettings _currentDrivingStartupSettings = null;
		private List<HistoryGpsEvent> _currentDrivingHistoryEvents = new List<HistoryGpsEvent>();

		public VirtualDrivingRealtimeInputViewModel(MainWindowViewModel parentViewModel)
		{
			ParentViewModel = parentViewModel;

			EditRoutesCommand = new RelayCommand(OpenEditDrivingRoutesView, () => EditDrivingRoutesButtonEnabled);
		}


		public MainWindowViewModel ParentViewModel { get; private set; }

		public WebView2 WebView => ParentViewModel.MainWindowInstance.virtualDrivingWebView;

		public bool IsDriving
		{
			get => _isDriving;
			set
			{
				SetProperty(ref _isDriving, value, nameof(IsDriving));
			}
		}

		public string StartOrStopVirtualDrivingButtonTitle
		{
			get
			{
				if (!IsDriving)
				{
					return "Start Virtual Driving";
				}

				return "Stop Virtual Driving";
			}
		}

		public string VirtualDrivingStartOrStopButtonImagePath
		{
			get
			{
				if (!IsDriving)
				{
					return "/Icons/StartGPSSimulation.png";
				}
				else
				{
					return "/Icons/StopGPSSimulation.png";
				}
			}
		}

		public bool EditDrivingRoutesButtonEnabled
		{
			get => !IsDriving;
		}

		public string EditDrivingRoutesButtonImagePath
		{
			get => "/Icons/RoutePath.png";
		}

		public IRelayCommand EditRoutesCommand { get; private set; }


		public void StartVirtualDrivingExecute(VirtualDrivingStartupSettings drivingStartupSettings)
		{
			try
			{
				if (IsDriving)
				{
					return;
				}

				_currentDrivingStartupSettings = drivingStartupSettings; // Save current driving startup settings

				// Clear existing driving history events and initialize mock gps event publisher
				_currentDrivingHistoryEvents.Clear();
				_mockGpsEventPublisher = new SingleDeviceMockGpsEventPublisher(drivingStartupSettings.GpsSimulationProfile);
				_mockGpsEventPublisher.InitializeResourcesForAllMockTargets();

				// Initialize cancel token source and start driving input tasks
				_drivingCancelTokenSource = new CancellationTokenSource();
				IsDriving = true;

				int fps = drivingStartupSettings.FramePerSecond;
				var usingCommonKeysOnly = drivingStartupSettings.ControlMethod == VirtualDrivingControlMethod.Gamepad;
				var interestedKeyset = DirectInputHelper.GetKeysetForVirtualDriving(usingCommonKeysOnly);

				_virtualDrivingInputTasks.Clear();
				var keyboardInputTask = Task.Run(async () =>
				{
					await DirectInputHelper.RunKeyboardInputLoopAsync(
						fps,
						interestedKeyset,
						keyStates => SendKeyboardInputToWebView(keyStates),
						_drivingCancelTokenSource.Token
						);
				});
				_virtualDrivingInputTasks.Add(keyboardInputTask);

				if (drivingStartupSettings.ControlMethod == VirtualDrivingControlMethod.Gamepad)
				{
					var gamepadInputTask = Task.Run(async () =>
					{
						await XInputHelper.RunGamepadInputLoopAsync(
							fps,
							gamepadStates => SendGamepadInputToWebView(gamepadStates),
							_drivingCancelTokenSource.Token
							);
					});

					_virtualDrivingInputTasks.Add(gamepadInputTask);
				}

				// Start driving in webview
				startDrivingInWebView(drivingStartupSettings);

				ParentViewModel.VirtualDrivingGpsEventReceived += OnVirtualDrivingGpsEventReceived;

				// Raise VM property changed events
				OnPropertyChanged(nameof(StartOrStopVirtualDrivingButtonTitle));
				OnPropertyChanged(nameof(VirtualDrivingStartOrStopButtonImagePath));
				OnPropertyChanged(nameof(EditDrivingRoutesButtonEnabled));
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
				IsDriving = true;
			}
		}

		public void StopVirtualDrivingExecute()
		{
			try
			{
				if (!IsDriving)
				{
					return;
				}

				// Cancel driving input tasks and dispose mock gps event publisher
				_drivingCancelTokenSource?.Cancel();
				ParentViewModel.VirtualDrivingGpsEventReceived -= OnVirtualDrivingGpsEventReceived;
				_mockGpsEventPublisher?.Dispose();

				stopDrivingInWebView();

				IsDriving = false;

				Task.WaitAll(_virtualDrivingInputTasks.ToArray(), TimeSpan.FromSeconds(1));
				_virtualDrivingInputTasks.Clear();

				// Try saving driving history events if auto-save enabled
				// otherwise, prompt for saving
				TrySavingVirtualDrivingHistoryEventsAsync().ConfigureAwait(false);

				// Raise VM property changed events
				OnPropertyChanged(nameof(StartOrStopVirtualDrivingButtonTitle));
				OnPropertyChanged(nameof(VirtualDrivingStartOrStopButtonImagePath));
				OnPropertyChanged(nameof(EditDrivingRoutesButtonEnabled));
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
				IsDriving = false;
			}
		}

		private void OpenEditDrivingRoutesView()
		{
			var editRouteDialog = new EditDrivingRouteDialog();
			var closeAction = () =>
			{
				editRouteDialog.DialogResult = true;
				editRouteDialog.Close();
			};

			using (var editRouteViewModel = new EditDrivingRouteViewModel(
				null,
				closeAction))
			{
				editRouteDialog.DataContext = editRouteViewModel;

				editRouteDialog.ShowDialog();
			}
		}

		private void startDrivingInWebView(VirtualDrivingStartupSettings drivingStartupSettings)
		{
			ParentViewModel.MainWindowInstance.WindowState = WindowState.Maximized;

			var drivingProfile = drivingStartupSettings.DrivingProfile;
			var usePredefinedRoute = drivingStartupSettings.UsePredefinedRoute;
			var routeConductType = drivingStartupSettings.RouteConductType.ToString();
			var selectedRouteName = drivingStartupSettings.SelectedRoute?.Name;

			var drivingRouteJsonStr = !usePredefinedRoute ? "null" : $@"{{
	""usePredefinedRoute"": {usePredefinedRoute.ToString().ToLower()},
	""routeConductType"": ""{routeConductType}"",
	""selectedRouteName"": ""{selectedRouteName}""
}}";
			var startupSettingsJsonObjectStr = $@"{{
""fps"": {drivingStartupSettings.FramePerSecond},
""controlMethod"": ""{drivingStartupSettings.ControlMethod.ToString()}"",
""drivingProfile"": {{
	""Acceleration"": {drivingProfile.Acceleration.ToString("#.##")},
	""Deceleration"": {drivingProfile.Deceleration.ToString("#.##")},
	""MaxSpeed"": {drivingProfile.MaxSpeed.ToString("#.##")},
	""DragCoefficient"": {drivingProfile.DragCoefficient.ToString("#.##")},
	""Mass"": {drivingProfile.Mass.ToString("#.##")},
	""DeltaAnglePerSecond"": {(int)drivingProfile.DeltaAnglePerSecond}
}},
""drivingRoute"": {drivingRouteJsonStr}
}}";

			WebView.Dispatcher.Invoke(() =>
			{
				var startVirtualDrivingScript = $@"
window.rootViewModel.startVirtualDriving({startupSettingsJsonObjectStr});
";
				WebView.CoreWebView2?.ExecuteScriptAsync(startVirtualDrivingScript);
			});
		}

		private void stopDrivingInWebView()
		{
			WebView.Dispatcher.Invoke(() =>
			{
				var stopVirtualDrivingScript = $@"
window.rootViewModel.stopVirtualDriving();
";
				WebView.CoreWebView2?.ExecuteScriptAsync(stopVirtualDrivingScript);
			});
		}

		private async Task TrySavingVirtualDrivingHistoryEventsAsync()
		{
			if (!_currentDrivingHistoryEvents.Any()) { return; }

			var autoSave = _currentDrivingStartupSettings.AutoSaveGpsEventsAfterDrivingComplete;
			var saveEventsDirectoryPath = _currentDrivingStartupSettings.AutoSaveGpsEventsDirectoryPath;
			var saveEventsFileName = ComposeDrivingHistoryEventsFileName(_currentDrivingHistoryEvents);

			if (!autoSave)
			{
				var result = MessageBox.Show("Do you want to save all history GPS Events during virtual driving?", "Save Driving History", MessageBoxButton.YesNo);
				if (result != MessageBoxResult.Yes)
				{
					return;
				}

				saveEventsDirectoryPath = DirectoryAndFileSelectionHelper.PromptForDirectorySelection(saveEventsDirectoryPath);
				if (string.IsNullOrEmpty(saveEventsDirectoryPath))
				{
					return;
				}
			}
			else if (!Directory.Exists(saveEventsDirectoryPath))
			{
				MessageBox.Show($"Directory {saveEventsDirectoryPath} does not exist. Please select a valid directory to save virtual driving history GPS Events.");
				saveEventsDirectoryPath = DirectoryAndFileSelectionHelper.PromptForDirectorySelection(saveEventsDirectoryPath);
				if (string.IsNullOrEmpty(saveEventsDirectoryPath))
				{
					return;
				}
			}

			var saveEventsFullPath = Path.Combine(saveEventsDirectoryPath, saveEventsFileName);
			var (exported, error) = await ExcelAndCsvDataHelper.SaveHistoryGpsEventsAsCsvFile(saveEventsFullPath, _currentDrivingHistoryEvents).ConfigureAwait(false);
			if (exported)
			{
				if (File.Exists(saveEventsFullPath))
				{
					var startInfo = new ProcessStartInfo()
					{
						Arguments = saveEventsDirectoryPath,
						FileName = "explorer.exe",
					};

					Process.Start(startInfo);
				}
			}
			else
			{
				MessageBox.Show($"Failed to save Virtual Driving history GPS Events. Error: {error}");
			}
		}

		private void OnVirtualDrivingGpsEventReceived(object? sender, HistoryGpsEvent evt)
		{
			if (evt != null)
			{
				//Debug.WriteLine($"longitude: {evt.Longitude}, latitude: {evt.Latitude}, speed: {evt.Speed}, heading: {evt.Heading}, SentOn: {evt.StartTimeValue}");
				try
				{
					_currentDrivingHistoryEvents.Add(evt);
					_mockGpsEventPublisher?.PublishNewMockGpsEventAsync(evt, evt.StartTime, null);
				}
				catch (Exception ex)
				{
					LogHelper.Error(ex);
				}
			}
		}

		private void SendKeyboardInputToWebView(DirectInputKeyState[] keyStates)
		{
			try
			{
				var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ssZ");
				var keyStatesString = string.Join(",", keyStates.Select(x => $"{{\"key\": \"{x.Key}\", \"IsPressed\": {x.IsPressed.ToString().ToLower()}, \"IsReleased\": {x.IsReleased.ToString().ToLower()}}}"));
				var evtData = $@"
{{
	""time"": ""{timestamp}"",
	""keyStates"": [{keyStatesString}]
}}
";
				WebView.Dispatcher.Invoke(() =>
				{
					var sendKeyboardInputScript = $@"
window.rootViewModel.handleKeyboardInput(
{evtData}
);
";
					WebView.CoreWebView2?.ExecuteScriptAsync(sendKeyboardInputScript);
				});
			}
			catch (Exception ex)
			{
				LogHelper.Error($"SendKeyboardInputToWebView. {ex}");
			}
		}

		private void SendGamepadInputToWebView(XInputGamepadStates gamepadStates)
		{
			try
			{
				var evtData = gamepadStates.ToJsonObjectString();
				
				WebView.Dispatcher.Invoke(() =>
				{
					var sendKeyboardInputScript = $@"
window.rootViewModel.handleGamepadInput(
{evtData}
);
";
					WebView.CoreWebView2?.ExecuteScriptAsync(sendKeyboardInputScript);
				});
			}
			catch (Exception ex)
			{
				LogHelper.Error($"SendGamepadInputToWebView. {ex}");
			}
		}

		private static string ComposeDrivingHistoryEventsFileName(List<HistoryGpsEvent> historyEvents)
		{
			var firstEvent = historyEvents.First();
			var lastEvent = historyEvents.Last();

			var startEventTimeAbbreviation = firstEvent.StartTime.ToString("yyyyMMddHHmmss");
			var endEventTimeAbbreviation = lastEvent.StartTime.ToString("yyyyMMddHHmmss");
			var fileName = $"VirtualDrivingHistory_{startEventTimeAbbreviation}_{endEventTimeAbbreviation}.csv";
			return fileName;
		}

	}
}
