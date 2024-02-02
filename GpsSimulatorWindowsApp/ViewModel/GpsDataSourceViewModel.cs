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
using Microsoft.IdentityModel.Tokens;
using System.Net;
using GpsSimulatorWindowsApp.Logging;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class GpsDataSourceViewModel : ObservableObject
	{
		private const string GpsSimulationProfileFileName = "GPS_SIMULATION_PROFILE.json";

		private static readonly int[] _baudRates = { 2400, 4800, 9600, 19200, 38400 };
		private static readonly JsonSerializerOptions prettyJsonSerializationOptions = new JsonSerializerOptions() { WriteIndented = true };

		private GpsSimulationProfile _currentGpsSimulationProfile;
		private SerialPortItemVM? _selectedSerialPort;
		private BaudRateItemVM? _selectedBaudRate;

		private bool _webEventSourceChecked; // If use web/http resource as event source
		private string _webGpsResourceApiUrl;
		private HttpRequestOptionsForWebGpsEventSource _webGpsResourceRequestOptions;
		private string _webGpsResourceRequestHeadersValue;
		private bool _usePatrolfinderHistoryEventsApi;
		private string _patrolfinderHistoryDeviceId;
		private string _patrolfinderHistoryStartTimeValue;
		private string _patrolfinderHistoryEndTimeValue;
		private bool _reverseGpsEventsFromWebResource;

		private bool _localFileEventSourceChecked; // If use local file as event source
		private LocalGpsDataFileType? _localGpsDataFileType;
		private string _localGpsDataFilePath;


		private bool _useMockSpeed;
		private bool _sendInvalidLocationOnPause;
		private bool _sendToVirtualSerialPort;
		private bool _enableLocalTcpHost;
		private string _localTcpHostInfo;
		private string _deviceIdForLocalTcpHost;
		private bool _sendToServerViaUdp;
		private string _serverUdpHostInfo;
		private string _deviceIdForServerUdp;
		private bool _sendToAndroidViaAdb;
		private bool _sendToAndroidViaUdp;
		private string _androidAdbSerialNumber;
		private string _androidUdpHostInfo;
		private bool _sendToIOSDevice;
		private string _iosDeviceUdid;

		private KeyValuePair<string, MultiDeviceSimulationTargetType> _selectedMultiDeviceSimulationTargetItem;
		private string _plusDbConnectionString;
		private short _plusDatabaseId;
		private int _plusGpsVendorId;
		private string _patrolfinderMultiDeviceServerUdpHostInfo;


		public GpsDataSourceViewModel(MainWindowViewModel parentVM, Action applyAction, Action cancelAction)
		{
			ParentViewModel = parentVM;

			// Initialize list data
			var serialPortNames = SerialPort.GetPortNames();
			var portItems = serialPortNames?.Select(p => new SerialPortItemVM(p)).ToList() ?? new List<SerialPortItemVM>();
			SerialPorts = new ObservableCollection<SerialPortItemVM>(portItems);
			SelectedSerialPort = SerialPorts.FirstOrDefault();

			var baudRateItems = _baudRates.Select(r => new BaudRateItemVM(r)).ToList();
			BaudRates = new ObservableCollection<BaudRateItemVM>(baudRateItems);
			SelectedBaudRate = BaudRates.FirstOrDefault(r => r.Value == 9600);

			MultiDeviceSimulationTargetItems = new ReadOnlyCollection<KeyValuePair<string, MultiDeviceSimulationTargetType>>(
				new List<KeyValuePair<string, MultiDeviceSimulationTargetType>>()
				{
				new KeyValuePair<string, MultiDeviceSimulationTargetType> ("None", MultiDeviceSimulationTargetType.None),
				new KeyValuePair<string, MultiDeviceSimulationTargetType> ("Plus Database", MultiDeviceSimulationTargetType.PlusDatabase),
				new KeyValuePair<string, MultiDeviceSimulationTargetType> ("Patrolfinder Server UDP", MultiDeviceSimulationTargetType.PatrolfinderServerUdp),
				}
			);
			SelectedMultiDeviceSimulationTargetItem = MultiDeviceSimulationTargetItems.First();


			WebEventSourceChecked = true;
			LocalFileEventSourceChecked = false;

			PlusVehicleItems = new ObservableCollection<PlusVehicleInputViewModel>();
			PatrolfinderServerUdpDeviceItems = new ObservableCollection<PatrolfinderServerUdpDeviceInputViewModel>();

			// Init properties 
			TryRestoringGpsSimulationConfigurationFromAppData();

			// Init Commands
			ExportWebGpsDataToLocalFileCommand = new AsyncRelayCommand<string>(ExportGpsEventsFromWebResourceToLocalFileAsync, _ => true);
			ValidatePlusDatabaseMultiDeviceInputCommand = new RelayCommand(ValidatePlusDatabaseMultiDeviceInputData, () => true);
			AddNewMultiDeviceInputItemCommand = new RelayCommand<string>(AddNewMultiDeviceInputItem, _ => true);

			ApplyDefaultPatrolfinderHistoryEventsApiArgumentsCommand = new RelayCommand(ApplyDefaultPatrolfinderHistoryEventsApiArguments, () => true);
			ConfigureRequestOptionsForWebGpsResourceCommand = new RelayCommand(ConfigureRequestOptionsForWebGpsResource, () => true);
			ConfigureNmeaSentenceOptionsCommand = new RelayCommand<string>(ConfigureNmeaSentenceOptions, _ => true);
			ValidateWebGpsResourceCommand = new AsyncRelayCommand(ValidateWebGpsResourceAsync, () => true);

			SelectLocalGpsDataFileCommand = new AsyncRelayCommand<string>(SelectLocalGpsDataFileAsync, _ => true);

			ApplyGpsDataSourceCommand = new RelayCommand(() =>
			{
				var validateError = ValidateConfigurationChanges();
				if (validateError == null)
				{
					applyAction();
				}
				else
				{
					MessageBox.Show(validateError);
				}
			}, () => true);
			CancelGpsDataSourceCommand = new RelayCommand(cancelAction, () => true);
		}

		public MainWindowViewModel ParentViewModel { get; set; }

		public GpsSimulationProfile CurrentSimulationProfile
		{
			get
			{
				return _currentGpsSimulationProfile;
			}
		}

		public bool SendToVirtualSerialPort
		{
			get => _sendToVirtualSerialPort;
			set
			{
				SetProperty(ref _sendToVirtualSerialPort, value, nameof(SendToVirtualSerialPort));
			}
		}

		public SerialPortItemVM? SelectedSerialPort
		{
			get => _selectedSerialPort;
			set
			{
				SetProperty(ref _selectedSerialPort, value, nameof(SelectedSerialPort));
			}
		}

		public ObservableCollection<SerialPortItemVM> SerialPorts { get; private set; }

		public BaudRateItemVM? SelectedBaudRate
		{
			get => _selectedBaudRate;
			set
			{
				SetProperty(ref _selectedBaudRate, value, nameof(SelectedBaudRate));
			}
		}

		public ObservableCollection<BaudRateItemVM> BaudRates { get; private set; }

		public bool EnableLocalTcpHost
		{
			get => _enableLocalTcpHost;
			set
			{
				SetProperty(ref _enableLocalTcpHost, value, nameof(EnableLocalTcpHost));
			}
		}

		public string LocalTcpHostInfo
		{
			get => _localTcpHostInfo;
			set
			{
				SetProperty(ref _localTcpHostInfo, value, nameof(LocalTcpHostInfo));
			}
		}

		public string DeviceIdForLocalTcpHost
		{
			get => _deviceIdForLocalTcpHost;
			set
			{
				SetProperty(ref _deviceIdForLocalTcpHost, value, nameof(DeviceIdForLocalTcpHost));
			}
		}

		public NmeaSentencePlaybackOptions LocalTcpHostNmeaOptions { get; private set; }

		public bool SendToServerViaUdp
		{
			get => _sendToServerViaUdp;
			set
			{
				SetProperty(ref _sendToServerViaUdp, value, nameof(SendToServerViaUdp));
			}
		}

		public string ServerUdpHostInfo
		{
			get => _serverUdpHostInfo;
			set
			{
				SetProperty(ref _serverUdpHostInfo, value, nameof(ServerUdpHostInfo));
			}
		}

		public string DeviceIdForServerUdp
		{
			get => _deviceIdForServerUdp;
			set
			{
				SetProperty(ref _deviceIdForServerUdp, value, nameof(DeviceIdForServerUdp));
			}
		}

		public NmeaSentencePlaybackOptions ServerUdpNmeaOptions { get; private set; }

		public bool SendToAndroidViaAdb
		{
			get => _sendToAndroidViaAdb;
			set
			{
				SetProperty(ref _sendToAndroidViaAdb, value, nameof(SendToAndroidViaAdb));
			}
		}

		public string AndroidAdbSerialNumber
		{
			get => _androidAdbSerialNumber;
			set
			{
				SetProperty(ref _androidAdbSerialNumber, value, nameof(AndroidAdbSerialNumber));
			}
		}

		public bool SendToAndroidViaUdp
		{
			get => _sendToAndroidViaUdp;
			set
			{
				SetProperty(ref _sendToAndroidViaUdp, value, nameof(SendToAndroidViaUdp));
			}
		}

		public string AndroidUdpHostInfo
		{
			get => _androidUdpHostInfo;
			set
			{
				SetProperty(ref _androidUdpHostInfo, value, nameof(AndroidUdpHostInfo));
			}
		}

		public bool SendToIOSDevice
		{
			get => _sendToIOSDevice;
			set
			{
				SetProperty(ref _sendToIOSDevice, value, nameof(SendToIOSDevice));
			}
		}

		public string IOSDeviceUdid
		{
			get => _iosDeviceUdid;
			set
			{
				SetProperty(ref _iosDeviceUdid, value, nameof(IOSDeviceUdid));
			}
		}

		public ReadOnlyCollection<KeyValuePair<string, MultiDeviceSimulationTargetType>> MultiDeviceSimulationTargetItems { get; }

		public KeyValuePair<string, MultiDeviceSimulationTargetType> SelectedMultiDeviceSimulationTargetItem
		{
			get => _selectedMultiDeviceSimulationTargetItem;
			set
			{
				SetProperty(ref _selectedMultiDeviceSimulationTargetItem, value, nameof(SelectedMultiDeviceSimulationTargetItem));
				OnPropertyChanged(nameof(PlusDatabaseMultiDeviceSectionVisibility));
				OnPropertyChanged(nameof(SendToPlusDatabaseMultiDevice));
				OnPropertyChanged(nameof(PatrolfinderServerUdpMultiDeviceSectionVisibility));
				OnPropertyChanged(nameof(SendToPatrolfinderServerUdpMultiDevice));
			}
		}

		public Visibility PlusDatabaseMultiDeviceSectionVisibility
		{
			get
			{
				if (SelectedMultiDeviceSimulationTargetItem.Value == MultiDeviceSimulationTargetType.PlusDatabase)
				{
					return Visibility.Visible;
				}
				else
				{
					return Visibility.Collapsed;
				}
			}
		}

		public bool SendToPlusDatabaseMultiDevice
		{
			get => SelectedMultiDeviceSimulationTargetItem.Value == MultiDeviceSimulationTargetType.PlusDatabase;
		}

		public string PlusDbConnectionString
		{
			get => _plusDbConnectionString;
			set
			{
				SetProperty(ref _plusDbConnectionString, value, nameof(PlusDbConnectionString));
			}
		}

		public short PlusDatabaseId
		{
			get => _plusDatabaseId;
			set
			{
				SetProperty(ref _plusDatabaseId, value, nameof(PlusDatabaseId));
			}
		}

		public int PlusGpsVendorId
		{
			get => _plusGpsVendorId;
			set
			{
				SetProperty(ref _plusGpsVendorId, value, nameof(PlusGpsVendorId));
			}
		}

		public ObservableCollection<PlusVehicleInputViewModel> PlusVehicleItems { get;  private set; }

		public Visibility PatrolfinderServerUdpMultiDeviceSectionVisibility
		{
			get
			{
				if (SelectedMultiDeviceSimulationTargetItem.Value == MultiDeviceSimulationTargetType.PatrolfinderServerUdp)
				{
					return Visibility.Visible;
				}
				else
				{
					return Visibility.Collapsed;
				}
			}
		}

		public bool SendToPatrolfinderServerUdpMultiDevice
		{
			get => SelectedMultiDeviceSimulationTargetItem.Value == MultiDeviceSimulationTargetType.PatrolfinderServerUdp;
		}

		public string PatrolfinderMultiDeviceServerUdpHostInfo
		{
			get => _patrolfinderMultiDeviceServerUdpHostInfo;
			set
			{
				SetProperty(ref _patrolfinderMultiDeviceServerUdpHostInfo, value, nameof(PatrolfinderMultiDeviceServerUdpHostInfo));
			}
		}

		public ObservableCollection<PatrolfinderServerUdpDeviceInputViewModel> PatrolfinderServerUdpDeviceItems { get; private set; }


		public IAsyncRelayCommand<string> ExportWebGpsDataToLocalFileCommand { get; private set; }

		public IRelayCommand ValidatePlusDatabaseMultiDeviceInputCommand { get; private set; }

		public IRelayCommand<string> AddNewMultiDeviceInputItemCommand { get; private set; }

		public IAsyncRelayCommand SelectLocalGpsDataFileCommand { get; private set; }

		public IRelayCommand SelectGpsDataSourceCommand { get; private set; }

		public IRelayCommand ApplyDefaultPatrolfinderHistoryEventsApiArgumentsCommand { get; private set; }

		public IRelayCommand ConfigureRequestOptionsForWebGpsResourceCommand { get; private set; }

		public IRelayCommand<string> ConfigureNmeaSentenceOptionsCommand { get; private set; }

		public IAsyncRelayCommand ValidateWebGpsResourceCommand { get; private set; }
		
		public IRelayCommand ApplyGpsDataSourceCommand { get; private set; }

		public IRelayCommand CancelGpsDataSourceCommand { get; private set; }

		public bool ShowWebResourceEventSourceConfigArea
		{
			get
			{
				return WebEventSourceChecked;
			}
		}

		public bool ShowLocalDataFileEventSourceConfigArea
		{
			get
			{
				return LocalFileEventSourceChecked;
			}
		}

		public bool LocalFileEventSourceChecked
		{
			get
			{
				return _localFileEventSourceChecked;
			}

			set
			{
				SetProperty(ref _localFileEventSourceChecked, value);
				OnPropertyChanged(nameof(ShowLocalDataFileEventSourceConfigArea));
			}
		}

		public string SelectedLocalGpsDataFilePath
		{
			get => _localGpsDataFilePath;
			private set
			{
				SetProperty(ref _localGpsDataFilePath, value, nameof(SelectedLocalGpsDataFilePath));
			}
		}

		public LocalGpsDataFileType? SelectedLocalGpsDataFileType
		{
			get => _localGpsDataFileType;
			set
			{
				SetProperty(ref _localGpsDataFileType, value, nameof(SelectedLocalGpsDataFileType));
				OnPropertyChanged(nameof(SelectedLocalGpsDataFileTypeName));
			}
		}

		public string SelectedLocalGpsDataFileTypeName
		{
			get
			{
				if (SelectedLocalGpsDataFileType.HasValue)
				{
					return SelectedLocalGpsDataFileType.Value.ToString();
				}

				return string.Empty;
			}
		}

		public bool WebEventSourceChecked
		{
			get
			{
				return _webEventSourceChecked;
			}

			set
			{
				SetProperty(ref _webEventSourceChecked, value);
				OnPropertyChanged(nameof(ShowWebResourceEventSourceConfigArea));
			}
		}

		public string WebGpsDataResourceUrlValue
		{
			get => _webGpsResourceApiUrl;
			set
			{
				SetProperty(ref _webGpsResourceApiUrl, value, nameof(WebGpsDataResourceUrlValue));
			}
		}

		public HttpRequestOptionsForWebGpsEventSource WebGpsResourceRequestOptions
		{
			get => _webGpsResourceRequestOptions;
			set
			{
				SetProperty(ref _webGpsResourceRequestOptions, value, nameof(WebGpsResourceRequestOptions));
				OnPropertyChanged(nameof(WebGpsResourceRequestOptionsDisplayValue));
			}
		}

		public string WebGpsResourceRequestOptionsDisplayValue
		{
			get
			{
				var jsonStr = JsonSerializer.Serialize(WebGpsResourceRequestOptions, prettyJsonSerializationOptions);
				return jsonStr;
			}
		}

		public bool UsePatrolfinderHistoryEventsApiAsWebResource
		{
			get => _usePatrolfinderHistoryEventsApi;
			set
			{
				SetProperty(ref _usePatrolfinderHistoryEventsApi, value, nameof(UsePatrolfinderHistoryEventsApiAsWebResource));
			}
		}

		public string HistoryDeviceIdValue
		{
			get => _patrolfinderHistoryDeviceId;
			set
			{
				SetProperty(ref _patrolfinderHistoryDeviceId, value, nameof(HistoryDeviceIdValue));
			}
		}

		public string HistoryStartDateTimeValue
		{
			get => _patrolfinderHistoryStartTimeValue;
			set
			{
				SetProperty(ref _patrolfinderHistoryStartTimeValue, value, nameof(HistoryStartDateTimeValue));
			}
		}
		
		public string HistoryEndDateTimeValue
		{
			get => _patrolfinderHistoryEndTimeValue;
			set
			{
				SetProperty(ref _patrolfinderHistoryEndTimeValue, value, nameof(HistoryEndDateTimeValue));
			}
		}

		public bool ReverseHistoryGpsEvents
		{
			get => _reverseGpsEventsFromWebResource;
			set
			{
				SetProperty(ref _reverseGpsEventsFromWebResource, value, nameof(ReverseHistoryGpsEvents));
			}
		}

		public int SendGpsEventDelayInMillionSeconds
		{
			get;
			set;
		}

		public bool SendInvalidLocationOnPause
		{
			get => _sendInvalidLocationOnPause;
			set
			{
				SetProperty(ref _sendInvalidLocationOnPause, value, nameof(SendInvalidLocationOnPause));
			}
		}

		public bool UseMockSpeed
		{
			get => _useMockSpeed;
			set
			{
				SetProperty(ref _useMockSpeed, value, nameof(UseMockSpeed));
			}
		}

		public decimal MinMockSpeed
		{
			get;
			set;
		}

		public decimal MaxMockSpeed
		{
			get;
			set;
		}

		public void BeginEditingGpsSimulationConfiguration()
		{
			var currentProfile = CurrentSimulationProfile;

			// Populate Editing fields for Serial Port
			SendToVirtualSerialPort = currentProfile.SendToVirtualSerialPort ?? true;

			var port = SerialPorts.FirstOrDefault(p => p.Name.Equals(currentProfile.SerialPortName, StringComparison.OrdinalIgnoreCase));
			SelectedSerialPort = port;

			var baudRate = BaudRates.FirstOrDefault(br => br.Value == currentProfile.SerialPortBaudRate);
			SelectedBaudRate = baudRate;

			// Populate Editing fields for Local TCP host
			EnableLocalTcpHost = currentProfile.EnableLocalTcpHost ?? false;
			LocalTcpHostInfo = currentProfile.LocalTcpHostInfo ?? string.Empty;
			DeviceIdForLocalTcpHost = !string.IsNullOrEmpty(currentProfile.DeviceIdForLocalTcpHost) ?
				currentProfile.DeviceIdForLocalTcpHost :
				NmeaDataHelper.CreateMockCradleGpsDeviceId();
			LocalTcpHostNmeaOptions = currentProfile.LocalTcpHostNmeaOptions ?? NmeaSentencePlaybackOptions.Default;

			// Populate Editing fields for Server UDP
			SendToServerViaUdp = currentProfile.SendToServerViaUdp ?? false;
			ServerUdpHostInfo = currentProfile.ServerUdpHostInfo;
			DeviceIdForServerUdp = !string.IsNullOrEmpty(currentProfile.DeviceIdForServerUdp) ?
				currentProfile.DeviceIdForServerUdp :
				NmeaDataHelper.CreateMockCradleGpsDeviceId();
			ServerUdpNmeaOptions = currentProfile.ServerUdpNmeaOptions ?? NmeaSentencePlaybackOptions.Default;

			// Populate Editing fields for Android target
			SendToAndroidViaAdb = currentProfile.SendToAndroidViaAdb ?? false;
			SendToAndroidViaUdp = currentProfile.SendToAndroidViaUdp ?? false;

			AndroidAdbSerialNumber = currentProfile.AndroidAdbSerialNumber;
			AndroidUdpHostInfo = currentProfile.AndroidUdpHostInfo;

			// Populate Editing fields for iOS target
			SendToIOSDevice = currentProfile.SendToIOSDevice ?? false;
			IOSDeviceUdid = currentProfile.IOSDeviceUdid;

			// Populate Editing fields for Multi-Device targets
			var configuredMultiDeviceTargetType = currentProfile.MultiDeviceSimulationTarget ?? MultiDeviceSimulationTargetType.None;
			SelectedMultiDeviceSimulationTargetItem = MultiDeviceSimulationTargetItems.First(x => x.Value == configuredMultiDeviceTargetType);

			if (configuredMultiDeviceTargetType == MultiDeviceSimulationTargetType.PlusDatabase)
			{
				// PlusDatabase related configuration
				PlusDbConnectionString = currentProfile.PlusDbConnectionString;
				PlusDatabaseId = currentProfile.PlusDatabaseId;
				PlusGpsVendorId = currentProfile.PlusGpsVendorId;
				if (currentProfile.PlusGpsVehicles?.Any() == true)
				{
					PlusVehicleItems.Clear();
					foreach (var vehicle in currentProfile.PlusGpsVehicles)
					{
						var vehicleInputVM = new PlusVehicleInputViewModel(this)
						{
							VehicleGpsId = vehicle.VehicleGpsId,
							GpsDataFileType = vehicle.GpsDataFileType,
							GpsDataFilePath = vehicle.GpsDataFilePath,
						};
						PlusVehicleItems.Add(vehicleInputVM);
					}
				}
			}

			if (configuredMultiDeviceTargetType == MultiDeviceSimulationTargetType.PatrolfinderServerUdp)
			{
				// Patrolfinder Server Udp related configuration
				PatrolfinderMultiDeviceServerUdpHostInfo = currentProfile.PatrolfinderMultiDeviceServerUdpHostInfo;
				if (currentProfile.PatrolfinderServerUdpDeviceItems?.Any() == true)
				{
					PatrolfinderServerUdpDeviceItems.Clear();
					foreach (var device in currentProfile.PatrolfinderServerUdpDeviceItems)
					{
						var deviceInputVM = new PatrolfinderServerUdpDeviceInputViewModel(this)
						{
							DeviceId = device.DeviceId,
							GpsDataFileType = device.GpsDataFileType,
							GpsDataFilePath = device.GpsDataFilePath,
							NmeaOptions = device.NmeaOptions ?? NmeaSentencePlaybackOptions.Default,
						};
						PatrolfinderServerUdpDeviceItems.Add(deviceInputVM);
					}
				}
			}


			// Populating Editing fields for Event DataSource
			if (currentProfile.DataSource == GpsDataSourceType.LocalFileEventSource)
			{
				WebEventSourceChecked = false;
				LocalFileEventSourceChecked = true;
			}
			else
			{
				WebEventSourceChecked = true;
				LocalFileEventSourceChecked = false;
			}

			// For Local File Event Source
			SelectedLocalGpsDataFileType = currentProfile.LocalGpsDataFileType;
			SelectedLocalGpsDataFilePath = currentProfile.LocalGpsDataFilePath ?? string.Empty;

			// For Web Event Source
			WebGpsDataResourceUrlValue = currentProfile.WebGpsDataResourceUrl ?? string.Empty;
			WebGpsResourceRequestOptions = currentProfile.WebGpsDataRequestOptions ?? HttpRequestOptionsForWebGpsEventSource.DefaultHttpRequestOptions;

			UsePatrolfinderHistoryEventsApiAsWebResource = currentProfile.UsePFDeviceHistoryAsWebEventSource ?? true;
			HistoryDeviceIdValue = currentProfile.HistoryDeviceId;
			HistoryStartDateTimeValue = currentProfile.HistoryStartDateTime.ToString("yyyy-MM-dd HH:mm:ss");
			HistoryEndDateTimeValue = currentProfile.HistoryEndDateTime.ToString("yyyy-MM-dd HH:mm:ss");
			ReverseHistoryGpsEvents = currentProfile.ReverseGpsEventsFromWebResource ?? false;

			// Other settings
			UseMockSpeed = currentProfile.UseMockSpeed;
			MinMockSpeed = currentProfile.MinMockSpeed;
			MaxMockSpeed = currentProfile.MaxMockSpeed;
			SendInvalidLocationOnPause = currentProfile.SendInvalidLocationOnPause;
			SendGpsEventDelayInMillionSeconds = currentProfile.SendDataDelayInMillionSeconds;

			// Trigger Observable Properties change events
			OnPropertyChanged(nameof(SendToVirtualSerialPort));
			OnPropertyChanged(nameof(EnableLocalTcpHost));
			OnPropertyChanged(nameof(LocalTcpHostInfo));
			OnPropertyChanged(nameof(DeviceIdForLocalTcpHost));
			OnPropertyChanged(nameof(SendToServerViaUdp));
			OnPropertyChanged(nameof(ServerUdpHostInfo));
			OnPropertyChanged(nameof(DeviceIdForServerUdp));
			OnPropertyChanged(nameof(SendToAndroidViaAdb));
			OnPropertyChanged(nameof(SendToAndroidViaUdp));
			OnPropertyChanged(nameof(AndroidAdbSerialNumber));
			OnPropertyChanged(nameof(AndroidUdpHostInfo));

			OnPropertyChanged(nameof(SelectedMultiDeviceSimulationTargetItem));
			OnPropertyChanged(nameof(SendToPlusDatabaseMultiDevice));
			OnPropertyChanged(nameof(PlusDbConnectionString));
			OnPropertyChanged(nameof(PlusDatabaseId));
			OnPropertyChanged(nameof(PlusGpsVendorId));
			OnPropertyChanged(nameof(PlusVehicleItems));
			OnPropertyChanged(nameof(SendToPatrolfinderServerUdpMultiDevice));

			OnPropertyChanged(nameof(WebEventSourceChecked));
			OnPropertyChanged(nameof(LocalFileEventSourceChecked));
			OnPropertyChanged(nameof(ShowWebResourceEventSourceConfigArea));
			OnPropertyChanged(nameof(ShowLocalDataFileEventSourceConfigArea));

			OnPropertyChanged(nameof(SelectedLocalGpsDataFileType));
			OnPropertyChanged(nameof(SelectedLocalGpsDataFileTypeName));
			OnPropertyChanged(nameof(SelectedLocalGpsDataFilePath));

			OnPropertyChanged(nameof(WebGpsDataResourceUrlValue));
			OnPropertyChanged(nameof(WebGpsResourceRequestOptions));
			OnPropertyChanged(nameof(WebGpsResourceRequestOptionsDisplayValue));
			OnPropertyChanged(nameof(UsePatrolfinderHistoryEventsApiAsWebResource));
			OnPropertyChanged(nameof(HistoryDeviceIdValue));
			OnPropertyChanged(nameof(HistoryStartDateTimeValue));
			OnPropertyChanged(nameof(HistoryEndDateTimeValue));
			OnPropertyChanged(nameof(ReverseHistoryGpsEvents));

			OnPropertyChanged(nameof(UseMockSpeed));
			OnPropertyChanged(nameof(SendInvalidLocationOnPause));
			OnPropertyChanged(nameof(SendGpsEventDelayInMillionSeconds));
		}

		public void EndEditingGpsSimulationConfiguration(bool applyChanges)
		{
			if (applyChanges)
			{
				var newSimulationProfile = new GpsSimulationProfile()
				{
					SendToVirtualSerialPort = SendToVirtualSerialPort,
					SerialPortName = SelectedSerialPort?.Name ?? string.Empty,
					SerialPortBaudRate = SelectedBaudRate?.Value ?? 9600,
					EnableLocalTcpHost = EnableLocalTcpHost,
					LocalTcpHostInfo = LocalTcpHostInfo,
					DeviceIdForLocalTcpHost = DeviceIdForLocalTcpHost,
					LocalTcpHostNmeaOptions = LocalTcpHostNmeaOptions,
					SendToServerViaUdp = SendToServerViaUdp,
					DeviceIdForServerUdp = DeviceIdForServerUdp,
					ServerUdpNmeaOptions = ServerUdpNmeaOptions,
					ServerUdpHostInfo = ServerUdpHostInfo,
					SendToAndroidViaAdb = SendToAndroidViaAdb,
					AndroidAdbSerialNumber = AndroidAdbSerialNumber,
					SendToAndroidViaUdp = SendToAndroidViaUdp,
					AndroidUdpHostInfo = AndroidUdpHostInfo,
					SendToIOSDevice = SendToIOSDevice,
					IOSDeviceUdid = IOSDeviceUdid,

					MultiDeviceSimulationTarget = SelectedMultiDeviceSimulationTargetItem.Value,

					DataSource = LocalFileEventSourceChecked ? GpsDataSourceType.LocalFileEventSource : GpsDataSourceType.WebEventSource,

					UseMockSpeed = UseMockSpeed,
					MinMockSpeed = MinMockSpeed,
					MaxMockSpeed = MaxMockSpeed,
					SendInvalidLocationOnPause = SendInvalidLocationOnPause,
					SendDataDelayInMillionSeconds = SendGpsEventDelayInMillionSeconds,
				};

				if (newSimulationProfile.DataSource == GpsDataSourceType.LocalFileEventSource)
				{
					newSimulationProfile.LocalGpsDataFileType = SelectedLocalGpsDataFileType;
					newSimulationProfile.LocalGpsDataFilePath = SelectedLocalGpsDataFilePath;
				}
				else if (newSimulationProfile.DataSource == GpsDataSourceType.WebEventSource)
				{
					newSimulationProfile.WebGpsDataRequestOptions = WebGpsResourceRequestOptions;
					newSimulationProfile.UsePFDeviceHistoryAsWebEventSource = UsePatrolfinderHistoryEventsApiAsWebResource;
					newSimulationProfile.HistoryDeviceId = HistoryDeviceIdValue;
					newSimulationProfile.HistoryStartDateTime = DateTime.Parse(HistoryStartDateTimeValue);
					newSimulationProfile.HistoryEndDateTime = DateTime.Parse(HistoryEndDateTimeValue);

					if (UsePatrolfinderHistoryEventsApiAsWebResource)
					{
						// Need to compose full WebGpsDataResourceUrl from patrolfinder specific parameters
						var patrolfinderWebGpsEventsBaseUrl = GpsEventPlaybackDataHelper.ExtractPFHistoryEventsBaseApiUrl(WebGpsDataResourceUrlValue);
						newSimulationProfile.WebGpsDataResourceUrl = GpsEventPlaybackDataHelper.ComposePatrolfinderHistoryEventsApiUrl(
							patrolfinderWebGpsEventsBaseUrl,
							newSimulationProfile.HistoryDeviceId,
							newSimulationProfile.HistoryStartDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
							newSimulationProfile.HistoryEndDateTime.ToString("yyyy-MM-dd HH:mm:ss")
							);
					}
					else
					{
						newSimulationProfile.WebGpsDataResourceUrl = WebGpsDataResourceUrlValue;
					}

					newSimulationProfile.ReverseGpsEventsFromWebResource = ReverseHistoryGpsEvents;
				}

				if (SendToPlusDatabaseMultiDevice)
				{
					newSimulationProfile.PlusDbConnectionString = PlusDbConnectionString;
					newSimulationProfile.PlusDatabaseId = PlusDatabaseId;
					newSimulationProfile.PlusGpsVendorId = PlusGpsVendorId;

					// Save the Plus Vehicle items into simulation profile's PlusGpsVehicles list
					newSimulationProfile.PlusGpsVehicles = new List<PlusGpsVehicleItem>();
					foreach (var vehicleInputVM in PlusVehicleItems)
					{
						newSimulationProfile.PlusGpsVehicles.Add(new PlusGpsVehicleItem()
						{
							VehicleGpsId = vehicleInputVM.VehicleGpsId,
							GpsDataFileType = vehicleInputVM.GpsDataFileType.Value,
							GpsDataFilePath = vehicleInputVM.GpsDataFilePath,
						});
					}
				}
				
				if (SendToPatrolfinderServerUdpMultiDevice)
				{
					newSimulationProfile.PatrolfinderMultiDeviceServerUdpHostInfo = PatrolfinderMultiDeviceServerUdpHostInfo;

					newSimulationProfile.PatrolfinderServerUdpDeviceItems = new List<PatrolfinderServerUdpDeviceItem>();
					foreach (var serverUdpDeviceInputVM in PatrolfinderServerUdpDeviceItems)
					{
						newSimulationProfile.PatrolfinderServerUdpDeviceItems.Add(new PatrolfinderServerUdpDeviceItem()
						{
							DeviceId = serverUdpDeviceInputVM.DeviceId,
							GpsDataFileType = serverUdpDeviceInputVM.GpsDataFileType.Value,
							GpsDataFilePath = serverUdpDeviceInputVM.GpsDataFilePath,
							NmeaOptions = serverUdpDeviceInputVM.NmeaOptions,
						});
					}
				}
				

				_currentGpsSimulationProfile = newSimulationProfile;

				SaveCurrentGpsSimulationConfigurationToAppData();
			}
		}

		public async Task ExportGpsEventsFromWebResourceToLocalFileAsync(string outputType)
		{
			var webResourceArgumentsError = ValidateWebGpsResourceArguments();
			if (!string.IsNullOrEmpty(webResourceArgumentsError))
			{
				MessageBox.Show(webResourceArgumentsError);
				return;
			}

			var webGpsDataResourceUrl = WebGpsDataResourceUrlValue;
			var startDateTime = DateTime.Parse(HistoryStartDateTimeValue);
			var endDateTime = DateTime.Parse(HistoryEndDateTimeValue);
			var reverseEvents = ReverseHistoryGpsEvents;
			var usePatrolfinderHistoryEventsApi = UsePatrolfinderHistoryEventsApiAsWebResource;
			if (usePatrolfinderHistoryEventsApi)
			{
				webGpsDataResourceUrl = GpsEventPlaybackDataHelper.ExtractPFHistoryEventsBaseApiUrl(WebGpsDataResourceUrlValue);
			}

			var historyEvents = await GpsEventPlaybackDataHelper.GetHistoryGpsEventsFromWebResourceAsync(
				webGpsDataResourceUrl,
				WebGpsResourceRequestOptions,
				usePatrolfinderHistoryEventsApi,
				HistoryDeviceIdValue,
				startDateTime,
				endDateTime,
				reverseEvents).ConfigureAwait(false);

			if (historyEvents?.Any() != true)
			{
				MessageBox.Show($"No history events found.");
				return;
			}

			var reverseFlag = reverseEvents ? "_Reversed" : string.Empty;
			var outputFileType = (LocalGpsDataFileType)Enum.Parse(typeof(LocalGpsDataFileType), outputType);
			var outputTypeKey = outputFileType.ToString().ToUpperInvariant();

			string fileExt = string.Empty, fileFilter = string.Empty;
			switch (outputFileType)
			{
				case LocalGpsDataFileType.NMEA:
					fileExt = "nmea";
					fileFilter = "NMEA Log|*.nmea|Text files|*.txt|All files|*.*";
					break;
				case LocalGpsDataFileType.CSV:
					fileExt = "csv";
					fileFilter = "CSV files|*.csv|Text files|*.txt|All files|*.*";
					break;
				case LocalGpsDataFileType.GPX:
					fileExt = "gpx";
					fileFilter = "GPX files|*.gpx|All files|*.*";
					break;
				default:
					throw new NotSupportedException($"Output file type {outputFileType} is not supported.");
			}

			var saveFileDialog = new Microsoft.Win32.SaveFileDialog()
			{
				Filter = fileFilter,
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
				FileName = $"{outputTypeKey}_PFDevice_{HistoryDeviceIdValue}_{startDateTime.ToString("yyyyMMddHHmmss")}_{endDateTime.ToString("yyyyMMddHHmmss")}{reverseFlag}.{fileExt}"
			};

			var result = saveFileDialog.ShowDialog();
			if (result == true)
			{
				var saveFilePath = saveFileDialog.FileName;
				bool exported = false;
				string error = null;
				switch (outputFileType)
				{
					case LocalGpsDataFileType.NMEA:
						(exported, error) = await NmeaDataHelper.SaveHistoryGpsEventsAsNmeaFileAsync(historyEvents, saveFilePath).ConfigureAwait(false);
						break;
					case LocalGpsDataFileType.CSV:
						(exported, error) = await ExcelAndCsvDataHelper.SaveHistoryGpsEventsAsCsvFile(saveFilePath, historyEvents).ConfigureAwait(false);
						break;
					case LocalGpsDataFileType.GPX:
						(exported, error) = await GPXDataHelper.SaveHistoryGpsEventsAsGPXFile(saveFilePath, historyEvents).ConfigureAwait(false);
						break;
				}

				if (exported)
				{
					var directoryPath = Path.GetDirectoryName(saveFilePath);
					if (Directory.Exists(directoryPath))
					{
						var startInfo = new ProcessStartInfo()
						{
							Arguments = directoryPath,
							FileName = "explorer.exe",
						};

						Process.Start(startInfo);
					}
				}
				else
				{
					MessageBox.Show($"Failed to export Web Gps Events to {saveFilePath}. Error: {error}");
				}
			}
		}

		public void ValidatePlusDatabaseMultiDeviceInputData()
		{
			string plusConfigValidateError = ValidatePlusDatabaseMultiDeviceConfigurationChanges();
			if (!string.IsNullOrEmpty(plusConfigValidateError))
			{
				MessageBox.Show(plusConfigValidateError);
			}
			else
			{
				MessageBox.Show("PLUS GPS VALIDATION PASSEDs!");
			}
		}

		public async Task SelectGpsDataFileForMultiDeviceItemAsync(MultiDeviceItemInputViewModel itemInputVM, LocalGpsDataFileType dataFileType)
		{
			if (itemInputVM == null)
			{
				return;
			}

			Microsoft.Win32.OpenFileDialog openFileDlg = new Microsoft.Win32.OpenFileDialog()
			{
				InitialDirectory = new System.IO.FileInfo(typeof(MainWindow).Assembly.Location).DirectoryName
			};

			switch (dataFileType)
			{
				case LocalGpsDataFileType.NMEA:
					openFileDlg.Filter = "Text files|*.txt|NMEA Log|*.nmea|All files|*.*";
					break;
				case LocalGpsDataFileType.CSV:
					openFileDlg.Filter = "CSV files|*.csv|Text files|*.txt|All files|*.*";
					break;
				case LocalGpsDataFileType.GPX:
					openFileDlg.Filter = "GPX files|*.gpx|All files|*.*";
					break;
				default:
					throw new NotSupportedException($"Local Gps File type {dataFileType} is not supported.");
			}

			string? selectedFilePath = null;
			var result = openFileDlg.ShowDialog();
			if (result.HasValue && result.Value || File.Exists(openFileDlg.FileName))
			{
				selectedFilePath = openFileDlg.FileName;
			}

			// TBD: need to add validation for selected file
			string? errorMessage = null;
			switch (dataFileType)
			{
				case LocalGpsDataFileType.NMEA:
					errorMessage = await NmeaDataHelper.TryValidatingNmeaDataFileAsync(selectedFilePath);
					break;
				case LocalGpsDataFileType.CSV:
					errorMessage = await ExcelAndCsvDataHelper.TryValidatingCsvFileAsync(selectedFilePath);
					break;
				case LocalGpsDataFileType.GPX:
					errorMessage = $"GPX file is not supported as Local EventSource now.";
					break;
			}

			if (string.IsNullOrEmpty(errorMessage))
			{
				itemInputVM.GpsDataFileType = dataFileType;
				itemInputVM.GpsDataFilePath = selectedFilePath;
			}
			else
			{
				MessageBox.Show(errorMessage);
			}
		}

		public void AddNewMultiDeviceInputItem(string? multiDeviceType)
		{
			switch (multiDeviceType)
			{
				case "PlusDatabase":
					var newPlusDatabaseDeviceVM = new PlusVehicleInputViewModel(this)
					{
						VehicleGpsId = string.Empty,
						GpsDataFileType = null,
						GpsDataFilePath = string.Empty
					};
					PlusVehicleItems.Add(newPlusDatabaseDeviceVM);
					break;
				case "PatrolfinderServerUdp":
					var newPatrolfinderServerUdpDeviceVM = new PatrolfinderServerUdpDeviceInputViewModel(this)
					{
						DeviceId = NmeaDataHelper.CreateMockCradleGpsDeviceId(),
						GpsDataFileType = null,
						GpsDataFilePath = string.Empty,
						NmeaOptions = NmeaSentencePlaybackOptions.Default,
					};
					PatrolfinderServerUdpDeviceItems.Add(newPatrolfinderServerUdpDeviceVM);
					break;
				default:
					MessageBox.Show($"Not supported multi-device type: {multiDeviceType}");
					break;
			}
		}

		public void RemoveMultiDeviceInputItem(MultiDeviceItemInputViewModel multiDeviceItemInputVM)
		{
			if (multiDeviceItemInputVM == null)
			{
				return;
			}

			if (multiDeviceItemInputVM is PlusVehicleInputViewModel)
			{
				var plusVehicleItem = multiDeviceItemInputVM as PlusVehicleInputViewModel;

				var dlgResult = MessageBox.Show("Are you sure to remove this GPS Vehicle item?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (dlgResult != MessageBoxResult.Yes)
				{
					return;
				}

				PlusVehicleItems.Remove(plusVehicleItem);
				plusVehicleItem.Dispose();
			}
			else if (multiDeviceItemInputVM is PatrolfinderServerUdpDeviceInputViewModel)
			{
				var pfserverUdpItem = multiDeviceItemInputVM as PatrolfinderServerUdpDeviceInputViewModel;

				var dlgResult = MessageBox.Show("Are you sure to remove this Patrolfinder Server UDP device?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (dlgResult != MessageBoxResult.Yes)
				{
					return;
				}

				PatrolfinderServerUdpDeviceItems.Remove(pfserverUdpItem);
				pfserverUdpItem.Dispose();
			}
			
		}

		public void ApplyDefaultPatrolfinderHistoryEventsApiArguments()
		{
			var tempSimulationProfile = new GpsSimulationProfile();
			tempSimulationProfile.ResetToDefaultWebGpsEventSource(); // generate a temp simulation profile with default PatrolfinderHistoryEventsApi arguments

			UsePatrolfinderHistoryEventsApiAsWebResource = true;
			WebGpsDataResourceUrlValue = tempSimulationProfile.WebGpsDataResourceUrl;
			WebGpsResourceRequestOptions = tempSimulationProfile.WebGpsDataRequestOptions;
			HistoryDeviceIdValue = tempSimulationProfile.HistoryDeviceId;
			HistoryStartDateTimeValue = tempSimulationProfile.HistoryStartDateTime.ToString("yyyy-MM-dd HH:mm:ss");
			HistoryEndDateTimeValue = tempSimulationProfile.HistoryEndDateTime.ToString("yyyy-MM-dd HH:mm:ss");
		}

		public void ConfigureRequestOptionsForWebGpsResource()
		{
			var modifyHeadersDialog = new ModifyHttpRequestOptionsDialog();
			modifyHeadersDialog.DataContext = new ModifyHttpRequestOptionsViewModel(
				WebGpsResourceRequestOptions,
				() =>
				{
					modifyHeadersDialog.DialogResult = true;
					modifyHeadersDialog.Close();
				},
				() =>
				{
					modifyHeadersDialog.DialogResult = false;
					modifyHeadersDialog.Close();
				}
				);
			
			var result = modifyHeadersDialog.ShowDialog();
			if (result == true)
			{
				WebGpsResourceRequestOptions = (modifyHeadersDialog.DataContext as ModifyHttpRequestOptionsViewModel).GetLatestRequestOptions();
			}
		}

		public void ConfigureNmeaSentenceOptions(string sourceOfOptions)
		{
			var forLocalTcpHost = sourceOfOptions == "LocalTcpHost";
			var forServerUdp = sourceOfOptions == "ServerUdp";

			var currentNmeaOptions = forLocalTcpHost ? LocalTcpHostNmeaOptions : forServerUdp ? ServerUdpNmeaOptions : null;
			if (currentNmeaOptions == null)
			{
				return;
			}

			var modifyNmeaOptionsDialog = new ModifyNmeaSentenceOptionsDialog();
			modifyNmeaOptionsDialog.DataContext = new ModifyNmeaSentenceOptionsViewModel(
				currentNmeaOptions,
				() =>
				{
					modifyNmeaOptionsDialog.DialogResult = true;
					modifyNmeaOptionsDialog.Close();
				},
				() =>
				{
					modifyNmeaOptionsDialog.DialogResult = false;
					modifyNmeaOptionsDialog.Close();
				}
				);

			var result = modifyNmeaOptionsDialog.ShowDialog();
			if (result == true)
			{
				var dialogVM = (modifyNmeaOptionsDialog.DataContext as ModifyNmeaSentenceOptionsViewModel);
				if (forLocalTcpHost)
				{
					LocalTcpHostNmeaOptions = dialogVM.GetLatestNmeaOptions();
				}

				if (forServerUdp)
				{
					ServerUdpNmeaOptions = dialogVM.GetLatestNmeaOptions();
				}
			}

		}

		public async Task ValidateWebGpsResourceAsync()
		{
			try
			{
				var webResourceArgumentsError = ValidateWebGpsResourceArguments();
				if (!string.IsNullOrEmpty(webResourceArgumentsError))
				{
					MessageBox.Show(webResourceArgumentsError);
					return;
				}

				var webGpsDataResourceUrl = WebGpsDataResourceUrlValue;
				var startDateTime = DateTime.Parse(HistoryStartDateTimeValue);
				var endDateTime = DateTime.Parse(HistoryEndDateTimeValue);
				var reverseEvents = ReverseHistoryGpsEvents;
				var usePatrolfinderHistoryEventsApi = UsePatrolfinderHistoryEventsApiAsWebResource;
				if (usePatrolfinderHistoryEventsApi)
				{
					webGpsDataResourceUrl = GpsEventPlaybackDataHelper.ExtractPFHistoryEventsBaseApiUrl(WebGpsDataResourceUrlValue);
				}

				var historyEvents = await GpsEventPlaybackDataHelper.GetHistoryGpsEventsFromWebResourceAsync(
					webGpsDataResourceUrl,
					WebGpsResourceRequestOptions,
					usePatrolfinderHistoryEventsApi,
					HistoryDeviceIdValue,
					startDateTime,
					endDateTime,
					reverseEvents).ConfigureAwait(false);

				var eventsFound = historyEvents?.Count ?? 0;
				MessageBox.Show($"Web GPS data request completed, obtained {eventsFound} events with current arguments.");
				return;
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to send Web GPS data request. Error: {ex.Message}");
			}
		}

		public async Task SelectLocalGpsDataFileAsync(string fileTypeName)
		{
			Microsoft.Win32.OpenFileDialog openFileDlg = new Microsoft.Win32.OpenFileDialog()
			{
				InitialDirectory = new System.IO.FileInfo(typeof(MainWindow).Assembly.Location).DirectoryName
			};

			var localGpsDataFileType = (LocalGpsDataFileType)Enum.Parse(typeof(LocalGpsDataFileType), fileTypeName);
			switch (localGpsDataFileType)
			{
				case LocalGpsDataFileType.NMEA:
					openFileDlg.Filter = "Text files|*.txt|NMEA Log|*.nmea|All files|*.*";
					break;
				case LocalGpsDataFileType.CSV:
					openFileDlg.Filter = "CSV files|*.csv|Text files|*.txt|All files|*.*";
					break;
				case LocalGpsDataFileType.GPX:
					openFileDlg.Filter = "GPX files|*.gpx|All files|*.*";
					break;
				default:
					throw new NotSupportedException($"Local Gps File type {fileTypeName} is not supported.");
			}

			string? selectedFilePath = null;
			var result = openFileDlg.ShowDialog();
			if (result.HasValue && result.Value || File.Exists(openFileDlg.FileName))
			{
				selectedFilePath = openFileDlg.FileName;
			}

			// TBD: need to add validation for selected file
			string? errorMessage = null;
			switch (localGpsDataFileType)
			{
				case LocalGpsDataFileType.NMEA:
					errorMessage = await NmeaDataHelper.TryValidatingNmeaDataFileAsync(selectedFilePath);
					break;
				case LocalGpsDataFileType.CSV:
					errorMessage = await ExcelAndCsvDataHelper.TryValidatingCsvFileAsync(selectedFilePath);
					break;
				case LocalGpsDataFileType.GPX:
					errorMessage = $"GPX file is not supported as Local EventSource now.";
					break;
			}

			if (string.IsNullOrEmpty(errorMessage))
			{
				SelectedLocalGpsDataFileType = localGpsDataFileType;
				SelectedLocalGpsDataFilePath = selectedFilePath;
			}
			else
			{
				MessageBox.Show(errorMessage);
			}
		}

		private string? ValidateWebGpsResourceArguments()
		{
			var errorBuffer = new StringBuilder();

			if (string.IsNullOrEmpty(HistoryDeviceIdValue))
			{
				errorBuffer.AppendLine("Invalid History Device Id");
			}

			DateTime startDateTime = DateTime.UtcNow, endDateTime = DateTime.UtcNow;
			if (string.IsNullOrEmpty(HistoryStartDateTimeValue) || !DateTime.TryParse(HistoryStartDateTimeValue, out startDateTime))
			{
				errorBuffer.AppendLine("Invalid History Start Date/Time");
			}

			if (string.IsNullOrEmpty(HistoryEndDateTimeValue) || !DateTime.TryParse(HistoryEndDateTimeValue, out endDateTime))
			{
				errorBuffer.AppendLine("Invalid History End Date/Time");
			}

			if (startDateTime >= endDateTime)
			{
				errorBuffer.AppendLine("History Start Date/Time must be less than History End Date/Time");
			}

			if (string.IsNullOrEmpty(WebGpsDataResourceUrlValue)
				|| (!WebGpsDataResourceUrlValue.StartsWith("http://") && !WebGpsDataResourceUrlValue.StartsWith("https://"))
				|| (UsePatrolfinderHistoryEventsApiAsWebResource && WebGpsDataResourceUrlValue.IndexOf("/patrolvehicleevents/device") <= 0))
			{
				errorBuffer.AppendLine("Invalid History Event Data Api Url");
			}

			return errorBuffer.Length > 0 ? errorBuffer.ToString() : null;
		}

		private string? ValidateLocalTcpHostChanges()
		{
			var errorBuffer = new StringBuilder();
			if (string.IsNullOrEmpty(LocalTcpHostInfo))
			{
				errorBuffer.AppendLine("Local Tcp Host Info(Format: \"{ip address}:{port}\") required!");
			}
			else
			{
				var hostFields = LocalTcpHostInfo.Split(':');
				if (hostFields.Length != 2)
				{
					errorBuffer.AppendLine("Invalid Local TCP Host Info. Host IP and Port(Format: \"{ip address}:{port}\") required!");
				}
				else
				{
					if (string.IsNullOrEmpty(hostFields[0]) || !IPAddress.TryParse(hostFields[0], out IPAddress _))
					{
						errorBuffer.AppendLine("Invalid Local TCP Host IP Address!");
					}

					if (!int.TryParse(hostFields[1], out int port) || port <= 1000)
					{
						errorBuffer.AppendLine("Invalid Server UDP Host Port!");
					}
				}
			}

			if (string.IsNullOrEmpty(DeviceIdForServerUdp))
			{
				errorBuffer.AppendLine("Device ID(for Local TCP Host) required");
			}

			return errorBuffer.Length > 0 ? errorBuffer.ToString() : null;
		}

		private string? ValidatePlusDatabaseMultiDeviceConfigurationChanges()
		{
			string connStr = PlusDbConnectionString;
			short databaseId = PlusDatabaseId;
			int gpsVendorId = PlusGpsVendorId;

			// Validate Plus Database connection string and check DatabaseId first
			if (string.IsNullOrEmpty(connStr))
			{
				return "Plus Database connection string required!";
			}

			var connectable = PlusDbQueryHelper.IsPlusDbConnectable(connStr);
			if (!connectable)
			{
				return "Plus Database(connection string) is not connectable!";
			}

			var allDataSources = PlusDbQueryHelper.GetDataSourceList(connStr);
			if (!allDataSources.Any())
			{
				return "Invalid Plus Database connection string or no DataSource found.";
			}

			if (!allDataSources.Any(ds => ds.Id == databaseId))
			{
				return $"No DataSource found which has DBID == {databaseId}";
			}

			if (PlusVehicleItems?.Any() != true)
			{
				return "At least one Plus GPS Vehicle required!";
			}

			// Validate the Gps Vendor Id then
			var allGpsVendors = PlusDbQueryHelper.GetGpsVendorList(connStr);
			if (!allGpsVendors.Any(v => v.Id == gpsVendorId))
			{
				return $"No Gps Vendor(Id == {gpsVendorId}) found in Plus Database.";
			}

			// Validate all Gps Vehicle items (VehicleGpsId and NmeaLogFilePath of each)
			var allGpsVehicleIds = PlusDbQueryHelper.GetUniqueVehicleGpsIdList(connStr, databaseId);
			var gpsIdSetInDb = new HashSet<string>(allGpsVehicleIds);
			var gpsIdSetInConfig = new HashSet<string>();
			var vehicleErrorBuffer = new StringBuilder();
			int lineNum = 0;
			foreach (var vehicleItem in PlusVehicleItems)
			{
				++lineNum;
				var itemErrorBuffer = new StringBuilder();
				string vehicleGpsId = vehicleItem.VehicleGpsId;
				if (string.IsNullOrEmpty(vehicleGpsId) || !gpsIdSetInDb.Contains(vehicleGpsId))
				{
					itemErrorBuffer.Append($"GpsId {vehicleGpsId} not found in DB, ");
				}
				else if (gpsIdSetInConfig.Contains(vehicleGpsId))
				{
					itemErrorBuffer.Append($"GpsId {vehicleGpsId} already exists in list, ");
				}

				gpsIdSetInConfig.Add(vehicleGpsId);

				if (!vehicleItem.GpsDataFileType.HasValue)
				{
					itemErrorBuffer.Append($"Gps Data File Type required, ");
				}

				string gpsDataFilePath = vehicleItem.GpsDataFilePath;
				if (string.IsNullOrEmpty(gpsDataFilePath) || !File.Exists(gpsDataFilePath))
				{
					itemErrorBuffer.Append($"Gps Data File not found.");
				}

				if (itemErrorBuffer.Length > 0)
				{
					vehicleErrorBuffer.AppendLine($"Index: {lineNum}, {itemErrorBuffer.ToString()}");
				}
			}

			if (vehicleErrorBuffer.Length > 0)
			{
				return vehicleErrorBuffer.ToString();
			}

			return null;
		}

		private string? ValidatePatrolfinderServerUdpMultiDeviceConfigurationChanges()
		{
			var errorBuffer = new StringBuilder();

			// Validate Server UDF Host Address and Port (should be of format: "(host name/ip):port"
			if (string.IsNullOrEmpty(ServerUdpHostInfo))
			{
				errorBuffer.AppendLine("Multi-Device Server UDP Host Info(Format: \"{ip address}:{port}\") required!");
			}
			else
			{
				var hostFields = ServerUdpHostInfo.Split(':');
				if (hostFields.Length != 2)
				{
					errorBuffer.AppendLine("Invalid Multi-Device Server UDP Host Info. Host IP and Port(Format: \"{ip address}:{port}\") required!");
				}
				else
				{
					if (string.IsNullOrEmpty(hostFields[0]))
					{
						errorBuffer.AppendLine("Invalid Multi-Device Server UDP IP Address!");
					}

					if (!int.TryParse(hostFields[1], out int port) || port <= 1000)
					{
						errorBuffer.AppendLine("Invalid Multi-Device Server UDP Port!");
					}
				}
			}

			if (PatrolfinderServerUdpDeviceItems?.Any() != true)
			{
				errorBuffer.AppendLine("At least one Server UDP Device item required!");
			}

			if (errorBuffer.Length > 0)
			{
				return errorBuffer.ToString();
			}

			// Validate each ServerUdp Device item
			int lineNum = 0;
			if (PatrolfinderServerUdpDeviceItems?.Any() == true)
			{
				++lineNum;
				foreach (var deviceItem in PatrolfinderServerUdpDeviceItems)
				{
					var itemErrorBuffer = new StringBuilder();
					if (string.IsNullOrEmpty(deviceItem.DeviceId))
					{
						itemErrorBuffer.AppendLine("Device ID required!");
					}

					string gpsDataFilePath = deviceItem.GpsDataFilePath;
					if (string.IsNullOrEmpty(gpsDataFilePath) || !File.Exists(gpsDataFilePath))
					{
						itemErrorBuffer.Append($"Gps Data File not found.");
					}

					if (itemErrorBuffer.Length > 0)
					{
						errorBuffer.AppendLine($"Index: {lineNum}, {itemErrorBuffer.ToString()}");
					}

				}
			}

			if (errorBuffer.Length > 0)
			{
				return $"Multi-Device Patrolfinder UDP Device Items:{Environment.NewLine}{errorBuffer.ToString()}";
			}

			return null;
		}

		private string? ValidateConfigurationChanges()
		{
			try
			{
				var errorBuffer = new StringBuilder();
				if (SendToVirtualSerialPort)
				{
					if (SelectedSerialPort == null)
					{
						errorBuffer.AppendLine("Port Name required!");
					}

					if (SelectedBaudRate == null)
					{
						errorBuffer.AppendLine("Port Baud Rate required!");
					}
				}

				if (EnableLocalTcpHost)
				{
					string? localTcpHostError = ValidateLocalTcpHostChanges();
					if (!string.IsNullOrEmpty(localTcpHostError))
					{
						errorBuffer.AppendLine(localTcpHostError);
					}
				}

				if (SendToServerViaUdp)
				{
					// Validate Server UDF Host Address and Port (should be of format: "(host name/ip):port"
					if (string.IsNullOrEmpty(ServerUdpHostInfo))
					{
						errorBuffer.AppendLine("Server UDP Host Info(Format: \"{ip address}:{port}\") required!");
					}
					else
					{
						var hostFields = ServerUdpHostInfo.Split(':');
						if (hostFields.Length != 2)
						{
							errorBuffer.AppendLine("Invalid Server UDP Host Info. Host IP and Port(Format: \"{ip address}:{port}\") required!");
						}
						else
						{
							if (string.IsNullOrEmpty(hostFields[0]))
							{
								errorBuffer.AppendLine("Invalid Server UDP Host IP Address!");
							}

							if (!int.TryParse(hostFields[1], out int port) || port <= 1000)
							{
								errorBuffer.AppendLine("Invalid Server UDP Host Port!");
							}
						}
					}

					if (string.IsNullOrEmpty(DeviceIdForServerUdp))
					{
						errorBuffer.AppendLine("Device ID(for Server UDP) required");
					}
				}

				if (SendToAndroidViaAdb)
				{
					// So far Device Serial Number is not required
				}

				if (SendToAndroidViaUdp)
				{
					// Validate UDF Host's IP Address and Port (should be of format: "Ipv4Address:port"
					if (string.IsNullOrEmpty(AndroidUdpHostInfo))
					{
						errorBuffer.AppendLine("Android UDP Host Info(Format: \"{ip address}:{port}\") required!");
					}
					else
					{
						var hostFields = AndroidUdpHostInfo.Split(':');
						if (hostFields.Length != 2)
						{
							errorBuffer.AppendLine("Invalid Android UDP Host Info. Host IP and Port(Format: \"{ip address}:{port}\") required!");
						}
						else
						{
							if (!System.Net.IPAddress.TryParse(hostFields[0], out System.Net.IPAddress ipAddress))
							{
								errorBuffer.AppendLine("Invalid Android UDP Host IP Address!");
							}

							if (!int.TryParse(hostFields[1], out int port) || port <= 1000)
							{
								errorBuffer.AppendLine("Invalid Android UDP Host Port!");
							}
						}
					}
				}

				if (SendToPlusDatabaseMultiDevice)
				{
					string? plusDbMultiDeviceConfigValidateError = ValidatePlusDatabaseMultiDeviceConfigurationChanges();
					if (!string.IsNullOrEmpty(plusDbMultiDeviceConfigValidateError))
					{
						errorBuffer.AppendLine(plusDbMultiDeviceConfigValidateError);
					}
				}

				if (SendToPatrolfinderServerUdpMultiDevice)
				{
					string? pfServerUdpMultiDeviceValidateError = ValidatePatrolfinderServerUdpMultiDeviceConfigurationChanges();
					if (!string.IsNullOrEmpty(pfServerUdpMultiDeviceValidateError))
					{
						errorBuffer.AppendLine(pfServerUdpMultiDeviceValidateError);
					}
				}

				if (SendGpsEventDelayInMillionSeconds < 500)
				{
					errorBuffer.AppendLine("Send Data Delay(ms) must >= 500");
				}

				if (LocalFileEventSourceChecked)
				{
					if (string.IsNullOrEmpty(SelectedLocalGpsDataFilePath) || !File.Exists(SelectedLocalGpsDataFilePath))
					{
						errorBuffer.AppendLine("Invalid Nmea Log File");
					}
				}

				if (WebEventSourceChecked)
				{
					var webResourceArgumentsError = ValidateWebGpsResourceArguments();
					if (!string.IsNullOrEmpty(webResourceArgumentsError))
					{
						errorBuffer.AppendLine(webResourceArgumentsError);
					}
				}

				if (UseMockSpeed)
				{
					if (MinMockSpeed < 0 || MaxMockSpeed < 0 || MinMockSpeed > MaxMockSpeed)
					{
						errorBuffer.AppendLine("MinMockSpeed must be <= MaxMockSpeed");
					}
				}

				if (errorBuffer.Length > 0)
				{
					return errorBuffer.ToString();
				}

				return null;
			}
			catch (Exception ex)
			{
				return ex.Message;
			}			
		}

		private void TryRestoringGpsSimulationConfigurationFromAppData()
		{
			try
			{
				TryMigratingLegacySimulationProfileFile();

				var simulationProfileFile = GetGpsSimulationProfileFilePath();
				if (File.Exists(simulationProfileFile))
				{

					var jsonData = File.ReadAllText(simulationProfileFile);
					var profileData = JsonSerializer.Deserialize<GpsSimulationProfile>(jsonData);
					if (profileData != null)
					{
						if (profileData.DataSource == GpsDataSourceType.LocalFileEventSource)
						{
							if (!profileData.LocalGpsDataFileType.HasValue || string.IsNullOrEmpty(profileData.LocalGpsDataFilePath))
							{
								profileData.ResetToDefaultLocalFileGpsEventSource();
							}
						}
						else
						{
							if (string.IsNullOrEmpty(profileData.WebGpsDataResourceUrl))
							{
								profileData.ResetToDefaultWebGpsEventSource();
							}
						}

						_currentGpsSimulationProfile = profileData;
					}
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error occurred when trying to restore Simulation Profile. {ex}");
				MessageBox.Show(ex.Message);
			}

			if (_currentGpsSimulationProfile == null)
			{
				_currentGpsSimulationProfile = new GpsSimulationProfile();
			}
		}

		private void SaveCurrentGpsSimulationConfigurationToAppData()
		{
			try
			{
				var simulationProfileFile = GetGpsSimulationProfileFilePath();
				using (var sw = new StreamWriter(simulationProfileFile, false))
				{
					var jsonData = JsonSerializer.Serialize(CurrentSimulationProfile);
					sw.Write(jsonData);
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error occurred when trying to save Simulation Profile. {ex}");
				MessageBox.Show(ex.Message);
			}
		}

		/// <summary>
		/// Compose the full path of the GPS Simulation Profile file
		/// </summary>
		/// <returns></returns>
		private static string GetGpsSimulationProfileFilePath()
		{
			var appDataDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var simulatorAppDataDirectoryPath = Path.Combine(appDataDirectoryPath, ApplicationConstants.ApplicationKey);
			if (!Directory.Exists(simulatorAppDataDirectoryPath))
			{
				Directory.CreateDirectory(simulatorAppDataDirectoryPath);
			}

			var profileFilePath = Path.Combine(simulatorAppDataDirectoryPath, GpsSimulationProfileFileName);
			return profileFilePath;
		}

		/// <summary>
		/// For old versio of GPS Simulator, the profile file is stored in different location
		/// We need to move it to the new location if only legacy profile file exists
		/// </summary>
		/// <returns></returns>
		private static void TryMigratingLegacySimulationProfileFile()
		{
			try
			{
				// Compose legacy simluation profile file path
				var appDataFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				var legacyProfileFilePath = Path.Combine(appDataFolderPath, GpsSimulationProfileFileName);

				if (!File.Exists(legacyProfileFilePath))
				{
					return; // No need to migrate
				}

				// Compose new simulation profile file path
				var newProfileFilePath = GetGpsSimulationProfileFilePath();
				if (!File.Exists(newProfileFilePath))
				{
					// Move legacy profile file to new location
					File.Move(legacyProfileFilePath, newProfileFilePath);
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error($"Error occurred when migrating legacy simulation profile file to new location. {ex}");
			}
		}
	}
}
