using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NmeaParser.Messages;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Dialogs;
using GpsSimulatorWindowsApp.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;


namespace GpsSimulatorWindowsApp.ViewModel
{
	public class MultiDeviceItemGpsEventPlaybackViewModel : ObservableObject
	{
		public string? VehicleGpsId { get; private set; }

		public string? DeviceId { get; private set; }

		public LocalGpsDataFileType GpsDataFileType { get; private set; }

		public string GpsDataFilePath { get; private set; }

		public NmeaSentencePlaybackOptions? NmeaOptions { get; private set; }

		private List<HistoryGpsEvent> _gpsEvents;
		private int _minPlaybackSliderValue = 0;
		private int _maxPlaybackSliderValue = 100;
		private int _currentPlaybackSliderValue = 0;
		private bool _showEventsOnMap;

		private MultiDeviceItemGpsEventPlaybackViewModel(MainWindowViewModel rootVM, LocalGpsDataFileType? gpsDataFileType, string? gpsDataFilePath)
		{
			if (rootVM == null)
			{
				throw new ArgumentNullException(nameof(rootVM));
			}

			if (gpsDataFileType == null)
			{
				throw new ArgumentNullException(nameof(gpsDataFileType));
			}

			if (string.IsNullOrEmpty(gpsDataFilePath) || !File.Exists(gpsDataFilePath))
			{
				throw new ArgumentNullException($"Invalid {nameof(gpsDataFilePath)}");
			}

			RootViewModel = rootVM;

			GpsDataFileType = gpsDataFileType.Value;
			GpsDataFilePath = gpsDataFilePath;
			ShowEventsOnMap = true;

			// Init Commands
			MoveToPreviousGpsEventCommand = new RelayCommand(() =>
			{
				MoveToPreviousGpsEvent();
			}, () => true);

			MoveToNextGpsEventCommand = new RelayCommand(() =>
			{
				MoveToNextGpsEvent();
			}, () => true);

			ShowOrHideEventsOnMapCommand = new RelayCommand(() =>
			{
				// MessageBox.Show($"ShowEventsOnMap of Vehicle: {VehicleGpsId} set to {ShowEventsOnMap}");
				var itemUniqueId = TargetType == MultiDeviceSimulationTargetType.PlusDatabase ? VehicleGpsId : DeviceId;
				RootViewModel.UpdateVisibilityOfDeviceMapLayerForMultiDeviceGpsPlayback(itemUniqueId, ShowEventsOnMap);
			}, () => true);
		}

		public MultiDeviceItemGpsEventPlaybackViewModel(PlusGpsVehicleItem gpsVehicleItem, MainWindowViewModel rootVM)
			: this(rootVM, gpsVehicleItem.GpsDataFileType, gpsVehicleItem.GpsDataFilePath)
		{
			if (string.IsNullOrEmpty(gpsVehicleItem?.VehicleGpsId))
			{
				throw new ArgumentNullException(nameof(gpsVehicleItem));
			}

			if (string.IsNullOrEmpty(gpsVehicleItem?.GpsDataFilePath) || !File.Exists(gpsVehicleItem.GpsDataFilePath))
			{
				throw new ArgumentNullException($"Invalid {nameof(gpsVehicleItem.GpsDataFilePath)}");
			}

			TargetType = MultiDeviceSimulationTargetType.PlusDatabase;
			VehicleGpsId = gpsVehicleItem.VehicleGpsId;
		}

		public MultiDeviceItemGpsEventPlaybackViewModel(PatrolfinderServerUdpDeviceItem serverUdpDeviceItem, MainWindowViewModel rootVM)
			: this(rootVM, serverUdpDeviceItem.GpsDataFileType, serverUdpDeviceItem.GpsDataFilePath)
		{
			if (string.IsNullOrEmpty(serverUdpDeviceItem?.DeviceId))
			{
				throw new ArgumentNullException(nameof(serverUdpDeviceItem));
			}

			if (string.IsNullOrEmpty(serverUdpDeviceItem?.GpsDataFilePath) || !File.Exists(serverUdpDeviceItem.GpsDataFilePath))
			{
				throw new ArgumentNullException($"Invalid {nameof(serverUdpDeviceItem.GpsDataFilePath)}");
			}

			TargetType = MultiDeviceSimulationTargetType.PatrolfinderServerUdp;
			DeviceId = serverUdpDeviceItem.DeviceId;
			NmeaOptions = serverUdpDeviceItem.NmeaOptions ?? NmeaSentencePlaybackOptions.Default;
		}

		public MultiDeviceSimulationTargetType TargetType { get; private set; }

		public string ItemUniqueId
		{
			get
			{
				switch (TargetType)
				{
					case MultiDeviceSimulationTargetType.PlusDatabase:
						return VehicleGpsId ?? string.Empty;
					case MultiDeviceSimulationTargetType.PatrolfinderServerUdp:
						return DeviceId ?? string.Empty;
					default:
						return "N/A";
				}
			}
		}

		public string ItemDescription
		{
			get
			{
				switch (TargetType)
				{
					case MultiDeviceSimulationTargetType.PlusDatabase:
						return $"Vehicle GPS ID: {VehicleGpsId}, Data File Type: {GpsDataFileType}";
					case MultiDeviceSimulationTargetType.PatrolfinderServerUdp:
						return $"Device ID: {DeviceId}, Data File Type: {GpsDataFileType}";
					default:
						return "N/A";
				}
			}
		}

		public MainWindowViewModel RootViewModel { get; private set; }

		public IRelayCommand MoveToPreviousGpsEventCommand { get; private set; }

		public IRelayCommand MoveToNextGpsEventCommand { get; private set; }

		public IRelayCommand ShowOrHideEventsOnMapCommand { get; private set; }

		public List<HistoryGpsEvent> PlaybackGpsEvents
		{ 
			get => _gpsEvents;
			private set
			{
				_gpsEvents = value;
			}
		}

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
				var currentEventTime = currentEvent.StartTime.ToString("HH:mm:ss");
				return $"Index: {CurrentPlaybackSliderValue}, Time: {currentEventTime}";
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

		public bool ShowEventsOnMap
		{
			get => _showEventsOnMap;
			set
			{
				SetProperty(ref _showEventsOnMap, value, nameof(ShowEventsOnMap));
			}
		}

		public async Task LoadGpsEventsAsync()
		{
			if (PlaybackGpsEvents?.Any() == true)
			{
				return; // Already loaded
			}

			List<HistoryGpsEvent>? gpsEventsFromDataFile = null;
			switch (GpsDataFileType)
			{
				case LocalGpsDataFileType.NMEA:
					gpsEventsFromDataFile = await NmeaDataHelper.GetHistoryGpsEventsFromNmeaFileAsync(GpsDataFilePath).ConfigureAwait(false);
					break;
				case LocalGpsDataFileType.CSV:
					gpsEventsFromDataFile = await ExcelAndCsvDataHelper.GetHistoryGpsEventsFromCsvFileAsync(GpsDataFilePath).ConfigureAwait(false);
					break;
				case LocalGpsDataFileType.GPX:
					throw new NotSupportedException("GPX file is not supported as Multi-Device GPS Playback Event source");
					break;
				default:
					throw new NotSupportedException($"Not supported GpsDataFileType: {GpsDataFileType}");
			}
			
			PlaybackGpsEvents = gpsEventsFromDataFile?.Where(evt => evt.Latitude.HasValue && evt.Longitude.HasValue)?.ToList() ?? new List<HistoryGpsEvent>();

			MinPlaybackSliderValue = 0;
			MaxPlaybackSliderValue = Math.Max(0, PlaybackGpsEvents.Count - 1);
			CurrentPlaybackSliderValue = MinPlaybackSliderValue;

			OnPropertyChanged(nameof(PlaybackEventRangeLabel));
		}

		public void ResetPlaybackPosition()
		{
			CurrentPlaybackSliderValue = MinPlaybackSliderValue;
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
	}
}
