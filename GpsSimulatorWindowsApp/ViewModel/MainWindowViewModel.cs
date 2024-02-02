using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Dialogs;
using GpsSimulatorWindowsApp.Helpers;
using GpsSimulatorWindowsApp.WebViewHost;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Navigation;
using System.Diagnostics;
using GpsSimulatorWindowsApp.Logging;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class MainWindowViewModel : ObservableObject, IDisposable
	{
		private const string _webViewHostDeviceObjectKey = "HostDevice";
		private const string VirtualDrivingBrowserProxyObjectKey = "VirtualDrivingBrowserProxy";

		public event EventHandler<HistoryGpsEvent> VirtualDrivingGpsEventReceived;

		private DateTime _lastSingleDeviceMapUpdateScriptExecutedTime = DateTime.MinValue;
		private DateTime _lastMultiDevicesMapUpdateScriptExecutedTime = DateTime.MinValue;
		private string _mainWindowTitle = string.Empty;
		private int _selectedMainTabItemIndex = 0;
		private bool _mainWindowLoaded = false;
		private string _deviceWebViewNavigationUrl = "https://web.local/index.html";
		private string _plusGpsMapWebViewNavigationUrl = "https://web.local/plusgpsmap.html";
		private string _virtualDrivingMapWebViewNavigationUrl = "https://web.local/vdrive/vdrivemap.html";
		private bool _singleDeviceMapWebViewInitialized = false;
		private bool _multiDevicesMapWebViewInitialized = false;
		private bool _virtualDrivingMapWebViewInitialized = false;


		private HostedDeviceComponent _webViewHostDeviceComponent;
		private VirtualDrivingWebViewProxy _virtualDrivingWebViewProxy;

		private GpsDataSourceConfigDialog _gpsDataSourceConfigDialog;

		private bool _disposed = false;

		public MainWindowViewModel()
		{
			// Init Geolocation simultion
			GpsDataSource = new GpsDataSourceViewModel(
				this,
				() =>
				{
					if (_gpsDataSourceConfigDialog != null)
					{
						_gpsDataSourceConfigDialog.DialogResult = true;
						_gpsDataSourceConfigDialog.Close();
					}
				},
				() =>
				{
					if (_gpsDataSourceConfigDialog != null)
					{
						_gpsDataSourceConfigDialog.DialogResult = false;
						_gpsDataSourceConfigDialog.Close();
					}
				});

			GpsPlayback = new GpsEventPlaybackViewModel(this);

			// Init Commands
			OpenLogDirectoryCommand = new RelayCommand(OpenLogFileDirectory);
			WebViewStartNavigationCommand = new AsyncRelayCommand(StartWebViewNavigationToUrlAsync, () => MainWindowLoaded);
			ConfigureGpsDataSourceCommand = new AsyncRelayCommand(ConfigureGpsDataSourceAsync, () => ConfigureGpsDataSourceButtonEnabled);

			GpsEventPlaybackStartOrPauseButtonCommand = new AsyncRelayCommand(
				StartOrPauseGpsEventPlaybackAsync,
				() => true
				);
			GpsEventPlaybackStopButtonCommand = new AsyncRelayCommand(StopGpsEventPlaybackAsyc, () => true);

			MoveToPreviousGpsEventCommand = new RelayCommand(() =>
			{
				GpsPlayback.MoveToPreviousGpsEvent();
			}, () => true);

			MoveToNextGpsEventCommand = new RelayCommand(() =>
			{
				GpsPlayback.MoveToNextGpsEvent();
			}, () => true);

			// Virtual Driving VM & commands
			VirtualDrivingRealtimeInput = new VirtualDrivingRealtimeInputViewModel(this);

			StartOrStopVirtualDrivingCommand = new RelayCommand(StartOrStopVirtualDriving, () => !GpsPlayback.SimulationStarted);

			// Initialize HostedDevice Component
			_webViewHostDeviceComponent = new HostedDeviceComponent(this);
			_virtualDrivingWebViewProxy = new VirtualDrivingWebViewProxy(this);

			// Init Window Title
			OnPropertyChanged(nameof(MainWindowTitle));
			GpsPlayback.UpdateGpsEventPlaybackContext(GpsDataSource.CurrentSimulationProfile);

			InitializeApplicationVersionInformation();
		}

		public bool MainWindowLoaded
		{
			get
			{
				return _mainWindowLoaded;
			}
		}

		public string DeviceWebViewNavigationUrl
		{
			get => _deviceWebViewNavigationUrl;
			set
			{
				SetProperty(ref _deviceWebViewNavigationUrl, value, nameof(DeviceWebViewNavigationUrl));
			}
		}

		public string PlusGpsMapWebViewNavigationUrl
		{
			get => _plusGpsMapWebViewNavigationUrl;
			set
			{
				SetProperty(ref _plusGpsMapWebViewNavigationUrl, value, nameof(PlusGpsMapWebViewNavigationUrl));
			}
		}

		public string VirtualDrivingMapWebViewNavigationUrl
		{
			get => _virtualDrivingMapWebViewNavigationUrl;
			set
			{
				SetProperty(ref _virtualDrivingMapWebViewNavigationUrl, value, nameof(VirtualDrivingMapWebViewNavigationUrl));
			}
		}

		public bool AllGpsMapWebViewInitialized
		{
			get => _singleDeviceMapWebViewInitialized && _multiDevicesMapWebViewInitialized && _virtualDrivingMapWebViewInitialized;
		}

		public MainWindow MainWindowInstance
		{
			get;
			private set;
		}

		public string MainWindowTitle
		{
			get
			{
				switch (SelectedMainTabItemIndex)
				{
					case 0:
						return GpsDataSource?.CurrentSimulationProfile?.GetSingleDeviceSimulationTitle() ?? string.Empty;
					case 1:
						return GpsDataSource?.CurrentSimulationProfile?.GetPlusSimulationTitle() ?? string.Empty;
					case 2:
						return "";
					default:
						return "";
				}
			}
		}

		public int SelectedMainTabItemIndex
		{
			get => _selectedMainTabItemIndex;
			set
			{
				SetProperty(ref _selectedMainTabItemIndex, value, nameof(SelectedMainTabItemIndex));
				OnPropertyChanged(nameof(MainWindowTitle));
				OnPropertyChanged(nameof(MainSimulationToolbarVisibility));
				OnPropertyChanged(nameof(VirtualDrivingToolbarVisibility));
			}
		}

		public Visibility MainSimulationToolbarVisibility
		{
			get
			{
				switch (SelectedMainTabItemIndex)
				{
					case 2:
						return Visibility.Hidden;
					default:
						return Visibility.Visible;
				}
			}
		}

		public Visibility VirtualDrivingToolbarVisibility
		{
			get
			{
				switch (SelectedMainTabItemIndex)
				{
					case 2:
						return Visibility.Visible;
					default:
						return Visibility.Hidden;
				}
			}
		}

		public Visibility VirtualDrivingTabItemVisibility
		{
			get
			{
				return GpsPlayback.SimulationStarted ? Visibility.Hidden : Visibility.Visible;
			}
		}

		public Visibility SingleDeviceSimulationTabItemVisibility
		{
			get
			{
				if (VirtualDrivingRealtimeInput.IsDriving)
				{
					return Visibility.Hidden;
				}

				if (GpsPlayback.SimulationStarted && SelectedMainTabItemIndex != 0)
				{
					return Visibility.Hidden;
				}

				return Visibility.Visible;
			}
		}

		public Visibility MultipleDevicesSimulationTabItemVisibility
		{
			get
			{
				if (VirtualDrivingRealtimeInput.IsDriving)
				{
					return Visibility.Hidden;
				}

				if (GpsPlayback.SimulationStarted && SelectedMainTabItemIndex != 1)
				{
					return Visibility.Hidden;
				}

				return Visibility.Visible;
			}
		}

		public GpsDataSourceViewModel GpsDataSource
		{
			get; private set;
		}

		public bool CanDeviceGpsPlayback
		{
			get => GpsPlayback.SimulationStarted;
		}

		public bool CanMultiDeviceGpsPlayback
		{
			get => GpsPlayback.SimulationStarted && GpsPlayback.MultiDeviceGpsPlaybackEnabled;
		}

		public GpsEventPlaybackViewModel GpsPlayback
		{
			get; private set;
		}

		public bool ConfigureGpsDataSourceButtonEnabled
		{
			get
			{
				return !GpsPlayback.SimulationStarted;
			}
		}

		public VirtualDrivingRealtimeInputViewModel VirtualDrivingRealtimeInput { get; private set; }

		public string StatusMessage
		{
			get
			{
				return string.Empty;
			}
		}

		public string InformationMessage
		{
			get
			{
				return string.Empty;
			}
		}


		public string ApplicationVersionSummary
		{
			get;
			private set;
		}

		#region -- Commands --

		public IRelayCommand OpenLogDirectoryCommand { get; private set; }

		public IAsyncRelayCommand WebViewStartNavigationCommand { get; private set; }

		public IAsyncRelayCommand ConfigureGpsDataSourceCommand { get; private set; }

		public IAsyncRelayCommand GpsEventPlaybackStartOrPauseButtonCommand { get; private set; }

		public IAsyncRelayCommand GpsEventPlaybackStopButtonCommand { get; private set; }

		public IRelayCommand MoveToPreviousGpsEventCommand { get; private set; }

		public IRelayCommand MoveToNextGpsEventCommand { get; private set; }

		public IRelayCommand StartOrStopVirtualDrivingCommand { get; private set; }

		#endregion

		public async Task SetMainWindowInstanceOnLoadedAsync(MainWindow windowInstance)
		{
			try
			{
				if (!_mainWindowLoaded)
				{
					MainWindowInstance = windowInstance;
					_mainWindowLoaded = true;

					// Initialize DeviceGpsMapWebView's CoreWebView engine
					var deviceGpsMapWebView = MainWindowInstance.deviceGpsMapWebView;
					await InitializeSingleDeviceGpsMapWebView2Async(deviceGpsMapWebView);
					deviceGpsMapWebView.CoreWebView2.Navigate(DeviceWebViewNavigationUrl);

					// Initialize multiDeviceGpsMapWebView's CoreWebView engine
					var multiDeviceGpsMapWebView = MainWindowInstance.multiDeviceGpsMapWebView;
					await InitializeMultiDevicesGpsMapWebView2Async(multiDeviceGpsMapWebView);
					multiDeviceGpsMapWebView.CoreWebView2.Navigate(PlusGpsMapWebViewNavigationUrl);
					multiDeviceGpsMapWebView.CoreWebView2.NavigationCompleted += (o, e) =>
					{
						// Do Plus/MultiDevice specific map initialization after CoreWebView2 is ready
						RecreateDeviceLayersForPlusGpsPlayback(GpsDataSource.CurrentSimulationProfile);
						GpsPlayback.SelectedMultiDeviceItemGpsPlaybackVM = GpsPlayback.MultiDeviceItemGpsPlaybackVMs.FirstOrDefault();
					};

					// Initialize VirtualDrivingMapWebView's CoreWebView engine
					var virtualDrivingMapWebView = MainWindowInstance.virtualDrivingWebView;
					await InitializeVirtualDrivingMapWebView2Async(virtualDrivingMapWebView);
					virtualDrivingMapWebView.CoreWebView2.Navigate(VirtualDrivingMapWebViewNavigationUrl);
					virtualDrivingMapWebView.CoreWebView2.NavigationCompleted += (o, e) =>
					{
						// TBD
					};

					OnPropertyChanged(nameof(MainWindowLoaded));
				}

				// Check ExternalTools
				IOSAutomationHelper.EnsureLibimobileToolExists();
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
			finally
			{
				await TryDismissingSplashscreenAsync();
			}
		}

		public void LoadAllPointsForDeviceGpsPlayback(List<HistoryGpsEvent> gpsEvents)
		{
			if (gpsEvents != null && gpsEvents.Count > 0)
			{
				var coordsStr = string.Join(",", gpsEvents.Select(evt => $"{{x: {evt.Longitude}, y: {evt.Latitude}}}"));
				var loadAllLocationsScript = $@"
window.rootViewModel.loadAllPlaybackLocations([{coordsStr}]);
";
				MainWindowInstance.Dispatcher.Invoke(async () =>
				{
					var webView = MainWindowInstance.deviceGpsMapWebView;
					webView.CoreWebView2?.ExecuteScriptAsync(loadAllLocationsScript);
				});
			}
		}

		public void UpdateCurrentLocationForDeviceGpsPlayback(HistoryGpsEvent gpsEvent, DateTime sentOn)
		{
			//if (gpsEvent != null && (DateTime.UtcNow - _lastSingleDeviceMapUpdateScriptExecutedTime).TotalMilliseconds >= 300)
			if (gpsEvent != null)
			{
				MainWindowInstance.Dispatcher.Invoke(() =>
				{
					var fixedAltitude = 1.0;
					var sentOnValue = sentOn.ToString("yyyy-MM-dd HH:mm:ssZ");
					var speedValue = gpsEvent.Speed.HasValue ? gpsEvent.Speed.Value.ToString("0.0") : string.Empty;
					var singleDeviceLocationUpdateScript = $@"
window.rootViewModel.updateCurrentLocation({gpsEvent.Longitude}, {gpsEvent.Latitude}, '{speedValue}', '{sentOnValue}');
";
					var webView = MainWindowInstance.deviceGpsMapWebView;
					webView.CoreWebView2?.ExecuteScriptAsync(singleDeviceLocationUpdateScript);
					_lastSingleDeviceMapUpdateScriptExecutedTime = DateTime.UtcNow;
				});
			}

		}

		public void UpdateSelectedDeviceForPlusGpsPlayback(string? deviceId)
		{
			MainWindowInstance.Dispatcher.Invoke(() =>
			{
				var updateSelectedDeviceScript = $@"
window.rootViewModel.selectDeviceItem('{deviceId ?? string.Empty}');";

				var webView = MainWindowInstance.multiDeviceGpsMapWebView;
				webView.CoreWebView2?.ExecuteScriptAsync(updateSelectedDeviceScript);
			});
		}

		public void RecreateDeviceLayersForPlusGpsPlayback(GpsSimulationProfile gpsSimulationProfile)
		{
			List<string> multiDeviceItemIds = new List<string>();
			if (gpsSimulationProfile.MultiDeviceSimulationTarget == MultiDeviceSimulationTargetType.PlusDatabase
				&& gpsSimulationProfile.PlusGpsVehicles.Any())
			{
				multiDeviceItemIds.AddRange(gpsSimulationProfile.PlusGpsVehicles.Select(v => v.VehicleGpsId));
			}
			else if(gpsSimulationProfile.MultiDeviceSimulationTarget == MultiDeviceSimulationTargetType.PatrolfinderServerUdp
				&& gpsSimulationProfile.PatrolfinderServerUdpDeviceItems.Any())
			{
				multiDeviceItemIds.AddRange(gpsSimulationProfile.PatrolfinderServerUdpDeviceItems.Select(v => v.DeviceId));
			}

			MainWindowInstance.Dispatcher.Invoke(() =>
			{
				var deviceIdArray = "[]";
				if (multiDeviceItemIds.Any())
				{
					deviceIdArray = $"[{string.Join(",", multiDeviceItemIds.Select(id => $"'{id}'"))}]";
				}

				var multiDevicesLocationUpdateScript = $@"
window.rootViewModel.recreateDevicePointsLayers({deviceIdArray})";

				var webView = MainWindowInstance.multiDeviceGpsMapWebView;
				webView.CoreWebView2?.ExecuteScriptAsync(multiDevicesLocationUpdateScript);
				_lastMultiDevicesMapUpdateScriptExecutedTime = DateTime.UtcNow;
			});
		}

		public void LoadPointsOfAllMultiDeviceItemsGpsPlayback(List<(string itemUniqueId, List<HistoryGpsEvent> gpsEvents)> playbackEventsOfAllDevices)
		{
			if (playbackEventsOfAllDevices.Any())
			{
				var loadPointsScriptBuffer = new StringBuilder();
				foreach (var playbackEventsOfSingleDevice in playbackEventsOfAllDevices)
				{
					if (playbackEventsOfSingleDevice.gpsEvents.Any())
					{
						var deviceId = playbackEventsOfSingleDevice.itemUniqueId;
						var coordsStr = string.Join(",", playbackEventsOfSingleDevice.gpsEvents.Select(evt => $"[{evt.Longitude}, {evt.Latitude}]"));
						loadPointsScriptBuffer.AppendLine($@"window.rootViewModel.loadPlaybackPointsOfSingleDevice('{deviceId}', [{coordsStr}]);");
					}
				}

				MainWindowInstance.Dispatcher.Invoke(() =>
				{
					var loadPointsScript = loadPointsScriptBuffer.ToString();
					var webView = MainWindowInstance.multiDeviceGpsMapWebView;
					webView.CoreWebView2?.ExecuteScriptAsync(loadPointsScript).ContinueWith(t =>
					{
						webView.Dispatcher.Invoke(() =>
						{
							// Try zoom to the paths of all playback devices after loading playback points on map
							var zoomToAllDevicePathsScript = "window.rootViewModel.zoomToPathsOfAllDevices();";
							webView.CoreWebView2?.ExecuteScriptAsync(zoomToAllDevicePathsScript);
						});
					});
				});
			}
		}

		public void UpdateCurrentLocationForMultiDeviceItemsGpsPlayback(List<(string itemUniqueId, HistoryGpsEvent gpsEvent)> multiDeviceItemsGpsEvents, DateTime sentOn)
		{
			if (multiDeviceItemsGpsEvents.Any() && (DateTime.UtcNow - _lastMultiDevicesMapUpdateScriptExecutedTime).TotalMilliseconds >= 500)
			{
				MainWindowInstance.Dispatcher.Invoke(() =>
				{

					// Convert multiDeviceItemsGpsEvents to JSON array and pass to JS function
					var objectsStr = string.Join(",", multiDeviceItemsGpsEvents.Select(veh => $"{{deviceId: '{veh.itemUniqueId}', longitude: {veh.gpsEvent.Longitude}, latitude: {veh.gpsEvent.Latitude}, speed: {veh.gpsEvent.Speed}}}"));
					var deviceItemArray = $"[{objectsStr}]";

					var sentOnValue = sentOn.ToString("yyyy-MM-dd HH:mm:ssZ");
					
					var multiDevicesLocationUpdateScript = $@"
window.rootViewModel.updateCurrentLocationOfDevices({deviceItemArray}, '{sentOnValue}')";

					var webView = MainWindowInstance.multiDeviceGpsMapWebView;
					webView.CoreWebView2?.ExecuteScriptAsync(multiDevicesLocationUpdateScript);
					_lastMultiDevicesMapUpdateScriptExecutedTime = DateTime.UtcNow;
				});
			}
		}

		public void UpdateVisibilityOfDeviceMapLayerForMultiDeviceGpsPlayback(string itemUniqueId, bool isVisible)
		{
			MainWindowInstance.Dispatcher.Invoke(() =>
			{
				var visibleValue = isVisible ? "true" : "false";
				var toggleDevicePlaybackLayerVisibilityScript = $@"
window.rootViewModel.toggleDevicePlaybackLayerVisibility('{itemUniqueId}', {visibleValue});";

				var webView = MainWindowInstance.multiDeviceGpsMapWebView;
				webView.CoreWebView2?.ExecuteScriptAsync(toggleDevicePlaybackLayerVisibilityScript);
			});
		}

		public async Task ConfigureGpsDataSourceAsync()
		{
			// Init Dialog
			_gpsDataSourceConfigDialog = new GpsDataSourceConfigDialog(GpsDataSource);
			GpsDataSource.BeginEditingGpsSimulationConfiguration();

			var dlgResult = _gpsDataSourceConfigDialog.ShowDialog();
			GpsDataSource.EndEditingGpsSimulationConfiguration(dlgResult == true);

			OnPropertyChanged(nameof(MainWindowTitle));
			GpsPlayback.UpdateGpsEventPlaybackContext(GpsDataSource.CurrentSimulationProfile);

			// Recreate device layers for MultiDevices GPS map and set Selected device to first
			RecreateDeviceLayersForPlusGpsPlayback(GpsDataSource.CurrentSimulationProfile);
			GpsPlayback.SelectedMultiDeviceItemGpsPlaybackVM = GpsPlayback.MultiDeviceItemGpsPlaybackVMs.FirstOrDefault();
		}

		public async Task StartWebViewNavigationToUrlAsync()
		{
			await MainWindowInstance.Dispatcher.InvokeAsync(() =>
			{
				var webView = MainWindowInstance.deviceGpsMapWebView;
				webView.CoreWebView2.Navigate(DeviceWebViewNavigationUrl);
			});
		}

		public async Task StartOrPauseGpsEventPlaybackAsync()
		{
			bool isSingleDeviceGpsPlayback = SelectedMainTabItemIndex == 0;
			bool isMultiDeviceGpsPlayback = SelectedMainTabItemIndex == 1;
			if (isSingleDeviceGpsPlayback)
			{
				await GpsPlayback.StartOrPauseSingleDeviceGpsPlaybackAsync(GpsDataSource.CurrentSimulationProfile);
			}
			else if (isMultiDeviceGpsPlayback)
			{
				await GpsPlayback.StartOrPauseMultiDeviceGpsPlaybackAsync(GpsDataSource.CurrentSimulationProfile);
			}
			else
			{
				return;
			}

			OnPropertyChanged(nameof(ConfigureGpsDataSourceButtonEnabled));
			OnPropertyChanged(nameof(CanDeviceGpsPlayback));
			OnPropertyChanged(nameof(CanMultiDeviceGpsPlayback));

			OnPropertyChanged(nameof(SingleDeviceSimulationTabItemVisibility));
			OnPropertyChanged(nameof(MultipleDevicesSimulationTabItemVisibility));
			OnPropertyChanged(nameof(VirtualDrivingTabItemVisibility));
		}

		public async Task StopGpsEventPlaybackAsyc()
		{
			bool isSingleDeviceGpsPlayback = SelectedMainTabItemIndex == 0;
			bool isMultiDeviceGpsPlayback = SelectedMainTabItemIndex == 1;
			if (isSingleDeviceGpsPlayback)
			{
				await GpsPlayback.StopSingleDeviceGpsPlaybackAsync();
			}
			else if (isMultiDeviceGpsPlayback)
			{
				await GpsPlayback.StopMultiDeviceGpsPlaybackAsync();
			}
			else
			{
				return;
			}

			OnPropertyChanged(nameof(ConfigureGpsDataSourceButtonEnabled));
			OnPropertyChanged(nameof(CanDeviceGpsPlayback));
			OnPropertyChanged(nameof(CanMultiDeviceGpsPlayback));

			OnPropertyChanged(nameof(SingleDeviceSimulationTabItemVisibility));
			OnPropertyChanged(nameof(MultipleDevicesSimulationTabItemVisibility));
			OnPropertyChanged(nameof(VirtualDrivingTabItemVisibility));
		}

		public void StartOrStopVirtualDriving()
		{
			if (GpsPlayback.SimulationStarted)
			{
				return;
			}

			if (VirtualDrivingRealtimeInput.IsDriving)
			{
				VirtualDrivingRealtimeInput.StopVirtualDrivingExecute();
			}
			else
			{
				// Launch a UI to let user select some prerequisites and customize options				
				var drivingStartupSettings = PromptForDrivingSettings();
				if (drivingStartupSettings == null)
				{
					return;
				}

				// Start the virtual driving
				VirtualDrivingRealtimeInput.StartVirtualDrivingExecute(drivingStartupSettings);
			}

			OnPropertyChanged(nameof(SingleDeviceSimulationTabItemVisibility));
			OnPropertyChanged(nameof(MultipleDevicesSimulationTabItemVisibility));
			OnPropertyChanged(nameof(VirtualDrivingTabItemVisibility));
		}

		private VirtualDrivingStartupSettings? PromptForDrivingSettings()
		{
			VirtualDrivingStartupSettings drivingStartupSettings = null;

			var simulationProfileCopy = GpsDataSource.CurrentSimulationProfile.DeepCopy();

			var drivingStartupSettingsDialog = new VirtualDrivingStartupDialog();
			drivingStartupSettingsDialog.DataContext = new VirtualDrivingStartupViewModel(
				simulationProfileCopy,
				() =>
				{
					drivingStartupSettingsDialog.DialogResult = true;
					drivingStartupSettingsDialog.Close();
				},
				() =>
				{
					drivingStartupSettingsDialog.DialogResult = false;
					drivingStartupSettingsDialog.Close();
				}
				);

			var result = drivingStartupSettingsDialog.ShowDialog();
			if (result == true)
			{
				var dialogVM = (drivingStartupSettingsDialog.DataContext as VirtualDrivingStartupViewModel);
				drivingStartupSettings = dialogVM.GetVirtualDrivingStartupSettings();
			}

			return drivingStartupSettings;
		}

		public void NotifyReceivedVirtualDrivingGpsEvent(HistoryGpsEvent newGpsEvent)
		{
			if (VirtualDrivingGpsEventReceived != null)
			{
				try
				{
					VirtualDrivingGpsEventReceived(this, newGpsEvent);
				}
				catch (Exception ex)
				{
					LogHelper.Error(ex);
				}
			}
		}

		private async Task InitializeSingleDeviceGpsMapWebView2Async(WebView2 webView)
		{
			var tabIndex = MainWindowInstance.simulationContentTab.SelectedIndex;
			MainWindowInstance.simulationContentTab.SelectedIndex = 0; // Make the tabItem of the WebView2 visible when initializing
			await webView.EnsureCoreWebView2Async();
			MainWindowInstance.simulationContentTab.SelectedIndex = tabIndex;

			// Mapping Virtual Host
			var localWebDir = System.IO.Path.Combine(AppContext.BaseDirectory, "LocalWeb");
			webView.CoreWebView2.SetVirtualHostNameToFolderMapping("web.local", localWebDir, CoreWebView2HostResourceAccessKind.Allow);

			// TODO: might need to include compile the following cache cleaning code for Debug build
			await webView.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.CacheStorage | CoreWebView2BrowsingDataKinds.DiskCache);

			// Inject hosted objects
			webView.CoreWebView2.AddHostObjectToScript(_webViewHostDeviceObjectKey, _webViewHostDeviceComponent);

			_singleDeviceMapWebViewInitialized = true;
			OnPropertyChanged(nameof(AllGpsMapWebViewInitialized));
		}

		private async Task InitializeMultiDevicesGpsMapWebView2Async(WebView2 webView)
		{
			var tabIndex = MainWindowInstance.simulationContentTab.SelectedIndex;
			MainWindowInstance.simulationContentTab.SelectedIndex = 1; // Make the tabItem of the WebView2 visible when initializing
			await webView.EnsureCoreWebView2Async();
			MainWindowInstance.simulationContentTab.SelectedIndex = tabIndex;

			// Mapping Virtual Host
			var localWebDir = System.IO.Path.Combine(AppContext.BaseDirectory, "LocalWeb");
			webView.CoreWebView2.SetVirtualHostNameToFolderMapping("web.local", localWebDir, CoreWebView2HostResourceAccessKind.Allow);

			// TODO: might need to include compile the following cache cleaning code for Debug build
			await webView.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.CacheStorage | CoreWebView2BrowsingDataKinds.DiskCache);

			// Inject hosted objects
			webView.CoreWebView2.AddHostObjectToScript(_webViewHostDeviceObjectKey, _webViewHostDeviceComponent);

			_multiDevicesMapWebViewInitialized = true;
			OnPropertyChanged(nameof(AllGpsMapWebViewInitialized));
		}

		private async Task InitializeVirtualDrivingMapWebView2Async(WebView2 webView)
		{
			var tabIndex = MainWindowInstance.simulationContentTab.SelectedIndex;
			MainWindowInstance.simulationContentTab.SelectedIndex = 2; // Make the tabItem of the WebView2 visible when initializing
			await webView.EnsureCoreWebView2Async();
			MainWindowInstance.simulationContentTab.SelectedIndex = tabIndex;

			// Mapping Virtual Host
			var localWebDir = System.IO.Path.Combine(AppContext.BaseDirectory, "LocalWeb");
			webView.CoreWebView2.SetVirtualHostNameToFolderMapping("web.local", localWebDir, CoreWebView2HostResourceAccessKind.Allow);

			// TODO: might need to include compile the following cache cleaning code for Debug build
			await webView.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.CacheStorage | CoreWebView2BrowsingDataKinds.DiskCache);

			// Inject hosted objects
			webView.CoreWebView2.AddHostObjectToScript(VirtualDrivingBrowserProxyObjectKey, _virtualDrivingWebViewProxy);


			_virtualDrivingMapWebViewInitialized = true;
			OnPropertyChanged(nameof(AllGpsMapWebViewInitialized));
		}

		private async Task TryDismissingSplashscreenAsync()
		{
			if (AllGpsMapWebViewInitialized && App.SplashScreen != null)
			{
				await Task.Delay(3000); // Delay for 3 seconds to make sure the map views are rendered 
				App.Current.TryDismissingSplashScreen();
			}
		}

		private void OpenLogFileDirectory()
		{
			try
			{
				var logFileDir = SerilogSetup.ApplicationLogDataDirectoryPath;
				if (!System.IO.Directory.Exists(logFileDir))
				{
					System.IO.Directory.CreateDirectory(logFileDir);
				}

				var startInfo = new ProcessStartInfo()
				{
					Arguments = logFileDir,
					FileName = "explorer.exe",
				};

				Process.Start(startInfo);

			}
			catch (Exception ex)
			{
				LogHelper.Error(ex);
				MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void InitializeApplicationVersionInformation()
		{
			var versionSummary = ClickOnceVersionHelper.GetVersionSummary();
			ApplicationVersionSummary = $"Version: {versionSummary}";
			OnPropertyChanged(nameof(ApplicationVersionSummary));
			LogHelper.Info($"ApplicationVersionSummary: {ApplicationVersionSummary}");
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			var deviceGpsMapWebView = MainWindowInstance.deviceGpsMapWebView;
			if (deviceGpsMapWebView != null)
			{
				deviceGpsMapWebView.Dispose();
			}

			var multiDeviceGpsMapWebView = MainWindowInstance.multiDeviceGpsMapWebView;
			if (multiDeviceGpsMapWebView != null)
			{
				multiDeviceGpsMapWebView.Dispose();
			}

			var virtualDrivingMapWebView = MainWindowInstance.virtualDrivingWebView;
			if (virtualDrivingMapWebView != null)
			{
				virtualDrivingMapWebView.Dispose();
			}

			_disposed = true;
		}
	}
}
