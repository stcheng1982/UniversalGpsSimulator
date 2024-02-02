using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NmeaParser.Messages;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Ports;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using GpsSimulatorWindowsApp.DataType.Network;
using System.Runtime.CompilerServices;
using GpsSimulatorWindowsApp.Logging;
using SharpDX.DirectInput;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class GpsEventPlaybackViewModel : ObservableObject
	{
		// public const string AppiumIoSettingAppNamespace = "io.appium.settings";
		public const string AndroidMockGpsAgentAppNamespace = "com.transfinder.mobile.android.mockgpsagent";

		private bool _multiDeviceGpsPlaybackEnabled;
		private ObservableCollection<MultiDeviceItemGpsEventPlaybackViewModel> _multiDeviceItemGpsPlaybackVMs = new ObservableCollection<MultiDeviceItemGpsEventPlaybackViewModel>();
		private MultiDeviceItemGpsEventPlaybackViewModel _selectedMultiDeviceItemGpsPlaybackVM = null;

		private int _minPlaybackSliderValue = 0;
		private int _maxPlaybackSliderValue = 100;
		private int _currentPlaybackSliderValue = 0;

		private bool _simulationStarted = false;
		private bool _isSendingMockGpsEvents = false;
		private CancellationTokenSource _singleDeviceGpsPlaybackCancelTokenSource = null;
		private CancellationTokenSource _multiDeviceGpsPlaybackCancelTokenSource = null;
		private SingleDeviceMockGpsEventPublisher _mockGpsEventPublisher = null;

		public GpsEventPlaybackViewModel(MainWindowViewModel parentVM)
		{
			ParentViewModel = parentVM;

			PlaybackGpsEvents = new List<HistoryGpsEvent>();

			// Commands
		}

		public MainWindowViewModel ParentViewModel { get; private set; }

		public bool MultiDeviceGpsPlaybackEnabled
		{
			get => _multiDeviceGpsPlaybackEnabled;
			set
			{
				SetProperty(ref _multiDeviceGpsPlaybackEnabled, value, nameof(MultiDeviceGpsPlaybackEnabled));
			}
		}

		public ObservableCollection<MultiDeviceItemGpsEventPlaybackViewModel> MultiDeviceItemGpsPlaybackVMs
		{
			get => _multiDeviceItemGpsPlaybackVMs;
			set
			{
				SetProperty(ref _multiDeviceItemGpsPlaybackVMs, value, nameof(MultiDeviceItemGpsPlaybackVMs));
			}
		}

		public MultiDeviceItemGpsEventPlaybackViewModel? SelectedMultiDeviceItemGpsPlaybackVM
		{
			get => _selectedMultiDeviceItemGpsPlaybackVM;
			set
			{
				SetProperty(ref _selectedMultiDeviceItemGpsPlaybackVM, value, nameof(SelectedMultiDeviceItemGpsPlaybackVM));
				OnPropertyChanged(nameof(SelectedMultiDeviceItemGpsPlaybackVMTitle));
				ParentViewModel.UpdateSelectedDeviceForPlusGpsPlayback(_selectedMultiDeviceItemGpsPlaybackVM?.ItemUniqueId);
			}
		}

		public string SelectedMultiDeviceItemGpsPlaybackVMTitle
		{
			get
			{
				if (SelectedMultiDeviceItemGpsPlaybackVM == null)
				{
					return string.Empty;
				}

				return SelectedMultiDeviceItemGpsPlaybackVM.ItemUniqueId;
			}
		}

		public List<HistoryGpsEvent> PlaybackGpsEvents { get; private set; }

		public int MinPlaybackSliderValue
		{
			get => _minPlaybackSliderValue;
			set
			{
				SetProperty(ref _minPlaybackSliderValue, value, nameof(MinPlaybackSliderValue));
			}
		}

		public int MaxPlaybackSliderValue
		{
			get => _maxPlaybackSliderValue;
			set
			{
				SetProperty(ref _maxPlaybackSliderValue, value, nameof(MaxPlaybackSliderValue));
			}
		}

		public int CurrentPlaybackSliderValue
		{
			get => _currentPlaybackSliderValue;
			set
			{
				SetProperty(ref _currentPlaybackSliderValue, value, nameof(CurrentPlaybackSliderValue));
				OnPropertyChanged(nameof(PlaybackCurrentEventLabel));
			}
		}

		public string PlaybackCurrentEventLabel
		{
			get
			{
				if (!PlaybackGpsEvents.Any()) return string.Empty;

				var currentEvent = PlaybackGpsEvents[CurrentPlaybackSliderValue];
				var currentEventTime = currentEvent.StartTime;
				var currentEventTimeLabel = currentEventTime == DateTime.MinValue ? currentEvent.StartTimeValue : currentEventTime.ToString("HH:mm:ss");
				return $"Index: {CurrentPlaybackSliderValue}, Time: {currentEventTimeLabel}";
			}
		}

		public string PlaybackEventRangeLabel
		{
			get
			{
				if (!PlaybackGpsEvents.Any()) return string.Empty;

				var firstEvent = PlaybackGpsEvents[MinPlaybackSliderValue];
				var lastEvent = PlaybackGpsEvents[MaxPlaybackSliderValue];
				
				var firstEventTime = firstEvent.StartTime.ToString("HH:mm:ss");
				var lastEventTime = lastEvent.StartTime.ToString("HH:mm:ss");
				return $"Range: [{MinPlaybackSliderValue} - {MaxPlaybackSliderValue}]({firstEventTime} - {lastEventTime})";
			}
		}

		public HistoryGpsEvent? CurrentPlaybackGpsEvent
		{
			get
			{
				if (PlaybackGpsEvents == null || CurrentPlaybackSliderValue >= PlaybackGpsEvents.Count)
				{
					return null;
				}

				return PlaybackGpsEvents[CurrentPlaybackSliderValue];
			}
		}

		public bool SimulationStarted
		{
			get => _simulationStarted;
			set
			{
				SetProperty(ref _simulationStarted, value, nameof(SimulationStarted));
				OnPropertyChanged(nameof(SimulationStartOrPauseButtonTitle));
				OnPropertyChanged(nameof(SimulationStartOrPauseButtonImagePath));
				OnPropertyChanged(nameof(SimulationStopButtonTitle));
				OnPropertyChanged(nameof(SimulationStopButtonImagePath));
			}
		}

		public bool IsSimulationEventStreamPaused
		{
			get => !_isSendingMockGpsEvents;
		}

		public string SimulationStartOrPauseButtonTitle
		{
			get
			{
				if (!_simulationStarted)
				{
					return "Start GPS Simulation";
				}
				else
				{
					return _isSendingMockGpsEvents ? "Pause GPS Simulation" : "Resume GPS Simulation";
				}
			}
		}

		public string SimulationStartOrPauseButtonImagePath
		{
			get
			{
				if (!_simulationStarted)
				{
					return "/Icons/StartGPSSimulation.png";
				}
				else
				{
					return _isSendingMockGpsEvents ? "/Icons/PauseGPSSimulation.png" : "/Icons/StartGPSSimulation.png";
				}
			}
		}

		public string SimulationStopButtonTitle
		{
			get
			{
				return "Stop GPS Simulation";
			}
		}

		public string SimulationStopButtonImagePath
		{
			get
			{
				return _simulationStarted ? "/Icons/StopGPSSimulation.png" : "/Icons/StopGPSSimulationGray.png";
			}
		}

		public void UpdateGpsEventPlaybackContext(GpsSimulationProfile? simulationProfile)
		{
			if (simulationProfile == null)
			{
				return;
			}

			// Clean up existing Plus vehicle playback VMs
			if (MultiDeviceItemGpsPlaybackVMs.Any())
			{
				foreach (var plusVehicleGpsPlaybackVM in MultiDeviceItemGpsPlaybackVMs)
				{
					plusVehicleGpsPlaybackVM.PlaybackGpsEvents?.Clear();
				}
			}

			MultiDeviceItemGpsPlaybackVMs.Clear();

			// Set new Plus Vehicle playback VMs
			if (simulationProfile.MultiDeviceSimulationTarget == MultiDeviceSimulationTargetType.PlusDatabase && simulationProfile.PlusGpsVehicles?.Any() == true)
			{
				foreach (var plusVehicle in simulationProfile.PlusGpsVehicles)
				{
					MultiDeviceItemGpsPlaybackVMs.Add(new MultiDeviceItemGpsEventPlaybackViewModel(plusVehicle, ParentViewModel));
				}

				MultiDeviceGpsPlaybackEnabled = true;
			}
			else if (simulationProfile.MultiDeviceSimulationTarget == MultiDeviceSimulationTargetType.PatrolfinderServerUdp && simulationProfile.PatrolfinderServerUdpDeviceItems?.Any() == true)
			{
				foreach (var pfServerUdpDevice in simulationProfile.PatrolfinderServerUdpDeviceItems)
				{
					MultiDeviceItemGpsPlaybackVMs.Add(new MultiDeviceItemGpsEventPlaybackViewModel(pfServerUdpDevice, ParentViewModel));
				}

				MultiDeviceGpsPlaybackEnabled = true;
			}
			else
			{
				MultiDeviceGpsPlaybackEnabled = false;
			}
		}

		public void MoveToPreviousGpsEvent()
		{
			int newSliderValue = _currentPlaybackSliderValue - 1;
			if (newSliderValue < MinPlaybackSliderValue)
			{
				newSliderValue = MaxPlaybackSliderValue;
			}

			CurrentPlaybackSliderValue = newSliderValue;
		}

		public void MoveToNextGpsEvent()
		{
			int newSliderValue = _currentPlaybackSliderValue + 1;
			if (newSliderValue > MaxPlaybackSliderValue)
			{
				newSliderValue = MinPlaybackSliderValue;
			}
			
			CurrentPlaybackSliderValue = newSliderValue;
		}

		public async Task StartOrPauseSingleDeviceGpsPlaybackAsync(GpsSimulationProfile gpsSimulationProfile)
		{
			if (!SimulationStarted)
			{
				try
				{
					await PrepareGpsEventsForSingleDeviceGpsPlayback(gpsSimulationProfile);

					_singleDeviceGpsPlaybackCancelTokenSource = new CancellationTokenSource();
					SimulationStarted = true;
					_isSendingMockGpsEvents = true;

					Task.Run(async () =>
					{
						await SendSingleDeviceGpsEventsAsync(gpsSimulationProfile, _singleDeviceGpsPlaybackCancelTokenSource.Token);
					});
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message);
				}
			}
			else
			{
				try
				{
					_isSendingMockGpsEvents = !_isSendingMockGpsEvents;
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message);
				}
			}

			// OnPropertyChanged(nameof(IsSimulationEventStreamPaused));
			OnPropertyChanged(nameof(SimulationStartOrPauseButtonTitle));
			OnPropertyChanged(nameof(SimulationStartOrPauseButtonImagePath));
			OnPropertyChanged(nameof(SimulationStopButtonImagePath));
		}

		public async Task StopSingleDeviceGpsPlaybackAsync()
		{
			try
			{
				if (SimulationStarted)
				{
					_singleDeviceGpsPlaybackCancelTokenSource.Cancel();
					while (SimulationStarted)
					{
						await Task.Delay(500);
					}

					_isSendingMockGpsEvents = false;
					_singleDeviceGpsPlaybackCancelTokenSource.Dispose();
					_singleDeviceGpsPlaybackCancelTokenSource = null;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}

			// OnPropertyChanged(nameof(IsSimulationEventStreamPaused));
			OnPropertyChanged(nameof(SimulationStartOrPauseButtonTitle));
			OnPropertyChanged(nameof(SimulationStartOrPauseButtonImagePath));
			OnPropertyChanged(nameof(SimulationStopButtonImagePath));
		}

		public async Task StartOrPauseMultiDeviceGpsPlaybackAsync(GpsSimulationProfile gpsSimulationProfile)
		{
			if (!SimulationStarted)
			{
				try
				{
					await PrepareGpsEventsForMultiDeviceGpsPlayback(gpsSimulationProfile);

					_multiDeviceGpsPlaybackCancelTokenSource = new CancellationTokenSource();
					SimulationStarted = true;
					_isSendingMockGpsEvents = true;

					Task.Run(async () =>
					{
						await SendMultiDevicesGpsEventsAsync(gpsSimulationProfile, _multiDeviceGpsPlaybackCancelTokenSource.Token);
					});
				}
				catch (Exception ex)
				{
					LogHelper.Error($"Error occurred when starting multi-device gps playback. {ex.ToString()}");
					MessageBox.Show(ex.Message);
				}
			}
			else
			{
				try
				{
					_isSendingMockGpsEvents = !_isSendingMockGpsEvents;
				}
				catch (Exception ex)
				{
					LogHelper.Error($"Error occurred when pausing or resuming multi-device gps playback. {ex.ToString()}");
					MessageBox.Show(ex.Message);
				}
			}

			// OnPropertyChanged(nameof(IsSimulationEventStreamPaused));
			OnPropertyChanged(nameof(SimulationStartOrPauseButtonTitle));
			OnPropertyChanged(nameof(SimulationStartOrPauseButtonImagePath));
			OnPropertyChanged(nameof(SimulationStopButtonImagePath));
		}

		public async Task StopMultiDeviceGpsPlaybackAsync()
		{
			try
			{
				if (SimulationStarted)
				{
					_multiDeviceGpsPlaybackCancelTokenSource.Cancel();
					while (SimulationStarted)
					{
						await Task.Delay(500);
					}

					_isSendingMockGpsEvents = false;
					_multiDeviceGpsPlaybackCancelTokenSource.Dispose();
					_multiDeviceGpsPlaybackCancelTokenSource = null;
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error occurred when stopping multi-device gps playback. {ex.ToString()}");
				MessageBox.Show(ex.Message);
			}

			// OnPropertyChanged(nameof(IsSimulationEventStreamPaused));
			OnPropertyChanged(nameof(SimulationStartOrPauseButtonTitle));
			OnPropertyChanged(nameof(SimulationStartOrPauseButtonImagePath));
			OnPropertyChanged(nameof(SimulationStopButtonImagePath));
		}

		private async Task PrepareGpsEventsForSingleDeviceGpsPlayback(GpsSimulationProfile gpsSimulationProfile)
		{
			PlaybackGpsEvents.Clear();
			var eventSourceType = gpsSimulationProfile.DataSource;
			if (eventSourceType == GpsDataSourceType.WebEventSource)
			{
				var reverseEvents = gpsSimulationProfile.ReverseGpsEventsFromWebResource ?? false;
				var webGpsDataResourceUrl = gpsSimulationProfile.WebGpsDataResourceUrl ?? string.Empty;
				var webGpsResourceRequestOptions = gpsSimulationProfile.WebGpsDataRequestOptions ?? HttpRequestOptionsForWebGpsEventSource.DefaultHttpRequestOptions;
				var usePatrolfinderHistoryEventsApi = gpsSimulationProfile.UsePFDeviceHistoryAsWebEventSource ?? true;
				if (usePatrolfinderHistoryEventsApi)
				{
					webGpsDataResourceUrl = GpsEventPlaybackDataHelper.ExtractPFHistoryEventsBaseApiUrl(webGpsDataResourceUrl);
				}
				var deviceHistoryEvents = await GpsEventPlaybackDataHelper.GetHistoryGpsEventsFromWebResourceAsync(
					webGpsDataResourceUrl,
					webGpsResourceRequestOptions,
					usePatrolfinderHistoryEventsApi,
					gpsSimulationProfile.HistoryDeviceId,
					gpsSimulationProfile.HistoryStartDateTime,
					gpsSimulationProfile.HistoryEndDateTime,
					reverseEvents);

				PlaybackGpsEvents.AddRange(deviceHistoryEvents);
			}
			else if (eventSourceType == GpsDataSourceType.LocalFileEventSource)
			{
				var localDataFileType = gpsSimulationProfile.LocalGpsDataFileType ?? LocalGpsDataFileType.NMEA;
				var localDataFilePath = gpsSimulationProfile.LocalGpsDataFilePath ?? string.Empty;
				List<HistoryGpsEvent> eventsFromLocalFile = null;
				switch (localDataFileType)
				{
					case LocalGpsDataFileType.NMEA:
						eventsFromLocalFile = await NmeaDataHelper.GetHistoryGpsEventsFromNmeaFileAsync(localDataFilePath);
						break;
					case LocalGpsDataFileType.CSV:
						eventsFromLocalFile = await ExcelAndCsvDataHelper.GetHistoryGpsEventsFromCsvFileAsync(localDataFilePath);
						break;
					case LocalGpsDataFileType.GPX:
						throw new NotImplementedException();
						break;
					default:
						throw new NotSupportedException($"LocalDataFileType: {localDataFileType} is not supported.");
				}

				if (eventsFromLocalFile?.Any() == true)
				{
					PlaybackGpsEvents.AddRange(eventsFromLocalFile);
				}
			}
			else
			{
				throw new NotSupportedException($"DataSource: {eventSourceType} is not supported.");
			}

			var gpsEventsToDisplay = PlaybackGpsEvents.Where(evt => evt.Latitude.HasValue && evt.Longitude.HasValue).ToList();
			ParentViewModel.LoadAllPointsForDeviceGpsPlayback(gpsEventsToDisplay);

			MinPlaybackSliderValue = 0;
			MaxPlaybackSliderValue = Math.Max(0, PlaybackGpsEvents.Count - 1);
			CurrentPlaybackSliderValue = MinPlaybackSliderValue;

			OnPropertyChanged(nameof(PlaybackCurrentEventLabel));
			OnPropertyChanged(nameof(PlaybackEventRangeLabel));
		}

		private async Task PrepareGpsEventsForMultiDeviceGpsPlayback(GpsSimulationProfile gpsSimulationProfile)
		{
			// If MultiDeviceGpsPlaybackEnabled is true, we load Playback GpsEvents for each configured Multi-Device playback item
			if (MultiDeviceGpsPlaybackEnabled)
			{
				var loadGpsEventTasks = new List<Task>();
				foreach (var multiDeviceGpsPlaybackVM in MultiDeviceItemGpsPlaybackVMs)
				{
					multiDeviceGpsPlaybackVM.ShowEventsOnMap = true; // Turn each vehicle's map visible to true
					loadGpsEventTasks.Add(multiDeviceGpsPlaybackVM.LoadGpsEventsAsync());
				}

				await Task.WhenAll(loadGpsEventTasks).ConfigureAwait(false);

				// Reset playback position of all PlusVehicleGpsPlaybackVMs
				foreach (var multiDeviceGpsPlaybackVM in MultiDeviceItemGpsPlaybackVMs)
				{
					multiDeviceGpsPlaybackVM.ResetPlaybackPosition();
				}

				// Collect the playbackGpsEvents of all PlusVehicleGpsPlaybackVMs and publish to Gps Map WebView
				var playbackEventsOfAllMultiDeviceItems = MultiDeviceItemGpsPlaybackVMs
					.Where(vm => vm.PlaybackGpsEvents?.Any() == true)
					.Select(vm => (vm.ItemUniqueId, vm.PlaybackGpsEvents))
					.ToList();
				ParentViewModel.LoadPointsOfAllMultiDeviceItemsGpsPlayback(playbackEventsOfAllMultiDeviceItems);
			}
		}

		private async Task SendSingleDeviceGpsEventsAsync(GpsSimulationProfile gpsSimulationProfile, CancellationToken cancellationToken)
		{
			try
			{
				// Initialize SingleDeviceMockGpsEventPublisher
				_mockGpsEventPublisher = new SingleDeviceMockGpsEventPublisher(gpsSimulationProfile);
				_mockGpsEventPublisher.InitializeResourcesForAllMockTargets();

				// Prepare arguments for Gps playback loop
				var delayMs = gpsSimulationProfile.SendDataDelayInMillionSeconds;
				var useMockSpeed = gpsSimulationProfile.UseMockSpeed;
				decimal minMockSpeed = gpsSimulationProfile.MinMockSpeed, maxMockSpeed = gpsSimulationProfile.MaxMockSpeed;
				var sendInvalidGpsOnPause = gpsSimulationProfile.SendInvalidLocationOnPause;

				HistoryGpsEvent? nextGpsEvent = null;
				DateTime lastSentOn = DateTime.UtcNow.AddMinutes(-1);
				while (!cancellationToken.IsCancellationRequested)
				{
					try
					{
						// Get the current GpsEvent to playback
						nextGpsEvent = CurrentPlaybackGpsEvent;
						if (nextGpsEvent == null)
						{
							LogHelper.Warn("Cannot obtain next GpsEvent to playback!!!!");
							await Task.Delay(1000);
							continue;
						}

						var msSinceLastSent = (int)(DateTime.UtcNow - lastSentOn).TotalMilliseconds;
						if (msSinceLastSent + 50 < delayMs)
						{
							await Task.Delay(delayMs - msSinceLastSent);
						}

						if (_isSendingMockGpsEvents)
						{
							// Is not paused, we move forward
							MoveToNextGpsEvent();
						}

						var sentOn = DateTime.UtcNow;
						decimal? mockSpeedValue = null;
						if (useMockSpeed)
						{
							var rnd = new Random();
							decimal randomOffset = Convert.ToDecimal(rnd.NextDouble()) * (maxMockSpeed - minMockSpeed);
							mockSpeedValue = minMockSpeed + randomOffset;
						}

						_mockGpsEventPublisher?.PublishNewMockGpsEventAsync(nextGpsEvent, sentOn, mockSpeedValue);

						lastSentOn = DateTime.UtcNow;
						PublishCurrentLocationForDeviceGpsPlayback(nextGpsEvent, sentOn);
					}
					catch (Exception ex)
					{
						LogHelper.Error($"Error in SendSingleDeviceGpsEventsAsync GPS Playback loop: {ex.ToString()}");
					}
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error in SendSingleDeviceGpsEventsAsync: {ex.ToString()}");
				MessageBox.Show(ex.Message);
			}
			finally
			{
				// Dispose SingleDeviceMockGpsEventPublisher
				_mockGpsEventPublisher?.Dispose();

				if (SimulationStarted)
				{
					SimulationStarted = false;
				}
			}
		}

		private async Task SendMultiDevicesGpsEventsAsync(GpsSimulationProfile gpsSimulationProfile, CancellationToken cancellationToken)
		{
			var multiDeviceTarget = gpsSimulationProfile.MultiDeviceSimulationTarget ?? MultiDeviceSimulationTargetType.None;
			bool sendToPlusDb = (multiDeviceTarget == MultiDeviceSimulationTargetType.PlusDatabase) && MultiDeviceItemGpsPlaybackVMs.Any(); // If we need to send mock event to Plus db
			string plusDbConnStr = null;
			TimeZoneInfo plusClientTimeZone = null;
			short plusDatabaseId = 7;
			int plusVendorId = 1;
			int plusVendorEventTypeId = 1;
			if (sendToPlusDb)
			{
				plusDbConnStr = gpsSimulationProfile.PlusDbConnectionString;
				plusDatabaseId = gpsSimulationProfile.PlusDatabaseId;
				plusVendorId = gpsSimulationProfile.PlusGpsVendorId;
				plusClientTimeZone = PlusDbQueryHelper.GetClientTimeZone(plusDbConnStr);
				plusVendorEventTypeId = PlusDbQueryHelper.GetMockGpsEventTypeId(plusDbConnStr, plusVendorId);
			}

			var sendToPatrolfinderServerUdp = (multiDeviceTarget == MultiDeviceSimulationTargetType.PatrolfinderServerUdp) && MultiDeviceItemGpsPlaybackVMs.Any();
			string pfServerUdpHostAddress = string.Empty;
			int pfServerUdpPort = 0;
			var nmeaOptionsDict = new Dictionary<string, NmeaSentencePlaybackOptions>();
			if (sendToPatrolfinderServerUdp)
			{
				var hostInfoFields = gpsSimulationProfile.PatrolfinderMultiDeviceServerUdpHostInfo?.Split(':');
				if (hostInfoFields?.Length == 2)
				{
					pfServerUdpHostAddress = hostInfoFields[0];
					if (int.TryParse(hostInfoFields[1], out int portValue))
					{
						pfServerUdpPort = portValue;
					}
				}

				foreach (var pfServerUdpDevice in gpsSimulationProfile.PatrolfinderServerUdpDeviceItems)
				{
					nmeaOptionsDict[pfServerUdpDevice.DeviceId] = pfServerUdpDevice.NmeaOptions ?? NmeaSentencePlaybackOptions.Default;
				}
			}

			try
			{
				var delayMs = gpsSimulationProfile.SendDataDelayInMillionSeconds;
				var useMockSpeed = gpsSimulationProfile.UseMockSpeed;
				decimal? mockSpeedValue = null;
				decimal minMockSpeed = gpsSimulationProfile.MinMockSpeed, maxMockSpeed = gpsSimulationProfile.MaxMockSpeed;
				var stopSendingGpsOnPause = gpsSimulationProfile.SendInvalidLocationOnPause;

				HistoryGpsEvent? nextGpsEvent = null;
				List<(string itemUniqueId, HistoryGpsEvent gpsEvent)>? nextMultiDeviceItemsGpsEvents = null;
				DateTime lastSentOn = DateTime.UtcNow.AddMinutes(-1);
				while (!cancellationToken.IsCancellationRequested)
				{
					try
					{
						if (!sendToPlusDb && !sendToPatrolfinderServerUdp)
						{
							// No need to send mock Gps events to Plus db or Patrolfinder Server Udp
							await Task.Delay(delayMs);
							continue;
						}

						// Ensure the delay time between each sent
						var msSinceLastSent = (int)(DateTime.UtcNow - lastSentOn).TotalMilliseconds;
						if (msSinceLastSent + 50 < delayMs)
						{
							await Task.Delay(delayMs - msSinceLastSent);
						}

						// Get the current GpsEvent to playback for each Device Item
						nextMultiDeviceItemsGpsEvents = MultiDeviceItemGpsPlaybackVMs
								.Where(v => v.CurrentPlaybackGpsEvent != null)
								.Select(v => (v.ItemUniqueId, v.CurrentPlaybackGpsEvent))
								.ToList() ?? new List<(string itemUniqueId, HistoryGpsEvent gpsEvent)>();

						if (_isSendingMockGpsEvents)
						{
							// Is not paused, we move forward the current GpsEvent of each Plus Gps Vehicle
							foreach (var multiDevicePlaybackVM in MultiDeviceItemGpsPlaybackVMs)
							{
								multiDevicePlaybackVM.MoveToNextGpsEvent();
							}
						}
						else
						{
							if (stopSendingGpsOnPause)
							{
								await Task.Delay(delayMs);
								continue; // Skip sending mock Gps events when paused and stopSendingGpsOnPause is true
							}
						}

						var sentOn = DateTime.UtcNow;

						if (sendToPlusDb)
						{
							// Send Mock GpsEvents of every PlusVehicle to Plus db
							await SaveMockGpsEventsIntoPlusDatabaseAsync(
								plusDbConnStr,
								plusClientTimeZone,
								plusDatabaseId,
								plusVendorId,
								plusVendorEventTypeId,
								nextMultiDeviceItemsGpsEvents,
								sentOn,
								mockSpeedValue
								);
						}

						if (sendToPatrolfinderServerUdp)
						{
							// Send Mock GpsEvents of every Device Item to Patrolfinder Server Udp
							SendMockGpsEventsToPatrolfinderServerUdp(
								pfServerUdpHostAddress,
								pfServerUdpPort,
								nextMultiDeviceItemsGpsEvents,
								sentOn,
								mockSpeedValue,
								nmeaOptionsDict
								);
						}

						lastSentOn = DateTime.UtcNow;
						PublishCurrentLocationForMultiDevicePlayback(nextMultiDeviceItemsGpsEvents, sentOn);
					}
					catch (Exception ex)
					{
						LogHelper.Error($"Error in SendMultiDevicesGpsEventsAsync's GPS Playback loop: {ex.ToString()}");
					}
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error in SendMultiDevicesGpsEventsAsync: {ex.ToString()}");
				MessageBox.Show(ex.Message);
			}
			finally
			{
				if (SimulationStarted)
				{
					SimulationStarted = false;
				}
			}
		}

		private void PublishCurrentLocationForDeviceGpsPlayback(HistoryGpsEvent gpsEvent, DateTime sentOn)
		{
			if (gpsEvent == null || gpsEvent.Longitude == null || gpsEvent.Latitude == null)
			{
				return;
			}

			ParentViewModel?.UpdateCurrentLocationForDeviceGpsPlayback(gpsEvent, sentOn);
		}

		private void PublishCurrentLocationForMultiDevicePlayback(List<(string itemUniqueId, HistoryGpsEvent gpsEvent)> multiDeviceItemsGpsEvents, DateTime sentOn)
		{
			if (!multiDeviceItemsGpsEvents.Any())
			{
				return;
			}

			ParentViewModel?.UpdateCurrentLocationForMultiDeviceItemsGpsPlayback(multiDeviceItemsGpsEvents, sentOn);
		}

		static async Task SaveMockGpsEventsIntoPlusDatabaseAsync(
			string connStr,
			TimeZoneInfo plusTimeZone,
			short dbid,
			int vendorId,
			int vendorEventTypeId,
			List<(string itemUniqueId, HistoryGpsEvent gpsEvent)> vehicleGpsEvents,
			DateTime sentOnUtc,
			decimal? mockSpeedValue)
		{
			try
			{
				var debugWatch = new Stopwatch();
				debugWatch.Start();

				//int utcOffsetInMinutesOfPlusClient = TimeZoneUtility.GetUtcOffsetInMinutesByTimeZoneId(plusTimeZone.Id, true);
				StringBuilder sqlBuffer = new StringBuilder();
				foreach (var vehicleEventData in vehicleGpsEvents)
				{
					// Need to prepare eventStartTime based on target Plus DB's client timezone
					var sentOnOfPlusClientTimeZone = TimeZoneInfo.ConvertTimeFromUtc(sentOnUtc, plusTimeZone);

					var singleInsertSql = SqlDbAccessHelper.ComposeInsertSqlForSingleNewPlusGpsEventItem(
						vendorId,
						dbid,
						vehicleEventData.itemUniqueId,
						vehicleEventData.gpsEvent,
						vendorEventTypeId,
						mockSpeedValue,
						sentOnOfPlusClientTimeZone,
						sentOnUtc);
					sqlBuffer.AppendLine(singleInsertSql);
				}

				if (sqlBuffer.Length > 0)
				{
					int execResult = await SqlDbAccessHelper.ExecuteNonQueryCommandAsync(connStr, sqlBuffer.ToString(), null).ConfigureAwait(false);
					LogHelper.Debug($"Inserted {vehicleGpsEvents.Count} GpsEvent records, Result: {execResult} ({debugWatch.ElapsedMilliseconds} ms) for Plus DB");
				}
				else
				{
					LogHelper.Debug($"No GpsEvent to insert for Plus DB");
				}

				debugWatch.Stop();
			}
			catch (Exception ex)
			{
				LogHelper.Error( $"Error occurred when try pushing gps data for Plus DB. {ex}");
			}
		}

		private void SendMockGpsEventsToPatrolfinderServerUdp(
			string pfServerUdpHostAddress,
			int pfServerUdpPort,
			List<(string itemUniqueId, HistoryGpsEvent gpsEvent)> nextMultiDeviceItemsGpsEvents,
			DateTime sentOn,
			decimal? mockSpeedValue,
			Dictionary<string, NmeaSentencePlaybackOptions> nmeaOptionsDict)
		{
			try
			{
				Parallel.ForEach(nextMultiDeviceItemsGpsEvents, multiDeviceEventItem =>
				{
					var deviceId = multiDeviceEventItem.itemUniqueId;
					var gpsEvent = multiDeviceEventItem.gpsEvent;
					var nmeaOptions = nmeaOptionsDict[multiDeviceEventItem.itemUniqueId];
					var dataBuf = new StringBuilder();

					if (nmeaOptions.GNGNSEnabled)
					{
						var gnsSentence = NmeaDataHelper.ComposeNmeaGnsSentence(gpsEvent, sentOn);
						dataBuf.Append(gnsSentence);
					}

					if (nmeaOptions.GPGGAEnabled)
					{
						var ggaSentence = NmeaDataHelper.ComposeNmeaGgaSentence(gpsEvent, sentOn);
						dataBuf.Append(ggaSentence);
					}

					if (nmeaOptions.GPGLLEnabled)
					{
						var gllSentence = NmeaDataHelper.ComposeNmeaGllSentence(gpsEvent, sentOn);
						dataBuf.Append(gllSentence);
					}

					if (nmeaOptions.GPRMCEnabled)
					{
						string rmcSentence;
						if (!mockSpeedValue.HasValue)
						{
							// nextGpsEvent.Speed.HasValue && nextGpsEvent.Speed.Value > 0.5m
							rmcSentence = NmeaDataHelper.ConvertHistoryGpsEventToNmeaRmcSentence(gpsEvent, sentOn);
						}
						else
						{
							rmcSentence = NmeaDataHelper.ConvertHistoryGpsEventToNmeaRmcSentence(gpsEvent, sentOn, mockSpeedValue.Value);
						}
						dataBuf.Append(rmcSentence);
					}

					if (nmeaOptions.GPVTGEnabled)
					{
						var vtgSentence = NmeaDataHelper.ComposeNmeaVtgSentence(gpsEvent, mockSpeedValue);
						dataBuf.Append(vtgSentence);
					}

					var deviceIdSentence = NmeaDataHelper.ComposeDeviceIdNmeaSentence(nmeaOptions.DeviceProfileName, deviceId);
					if (!string.IsNullOrEmpty(deviceIdSentence))
					{
						dataBuf.Append(deviceIdSentence);
					}

					SendUdpDatagram(pfServerUdpHostAddress, pfServerUdpPort, dataBuf.ToString());
				});
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error occurred when try sending Mock GPS Events to Patrolfinder Server UDP host. {ex}");
			}
		}

		private static void SendUdpDatagram(string udpAddress, int udpPort, string data)
		{
			using (var udpClient = new UdpClient())
			{
				var bytes = Encoding.ASCII.GetBytes(data);
				udpClient.Send(bytes, bytes.Length, udpAddress, udpPort);
			}
		}
	}
}
