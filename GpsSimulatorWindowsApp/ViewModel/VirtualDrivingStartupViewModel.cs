using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GpsSimulatorComponentLibrary.GameEngine;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Dialogs;
using GpsSimulatorWindowsApp.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;


namespace GpsSimulatorWindowsApp.ViewModel
{
	public class VirtualDrivingStartupViewModel : ObservableObject
	{
		public static string DefaultAutoSaveGpsEventsDirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "VirtualDrivingHistoryData");


		private Action _applyAction;
		private Action _cancelAction;

		private int _fps;
		private KeyValuePair<string, VirtualDrivingControlMethod> _selectedControlMethodItem;
		private VirtualDrivingProfile _selectedDrivingProfile;
		private bool _useCustomDrivingProfile;
		private decimal _customAcceleration;
		private decimal _customDeceleration;
		private decimal _customMaxSpeed;
		private decimal _customDragCoefficient;
		private decimal _customMass;
		private decimal _customDeltaAnglePerSecond;

		private bool _sendDrivingGpsEventToDeviceMockTarget;
		private bool _autoSaveGpsEventsAfterDrivingComplete;
		private string _autoSaveGpsEventsDirectoryPath;

		private bool _usePredefinedRoute;
		private KeyValuePair<string, VirtualDrivingRouteConductType> _selectedRouteConductTypeItem;
		private VirtualDrivingRoute _selectedDrivingRoute;

		public VirtualDrivingStartupViewModel(
			GpsSimulationProfile gpsSimulationProfile,
			Action applyAction,
			Action cancelAction)
		{
			DeviceSimulationProfile = gpsSimulationProfile;
			DeviceSimulationProfile.SendToVirtualSerialPort = DeviceSimulationProfile.SendToVirtualSerialPort ?? false;
			DeviceSimulationProfile.EnableLocalTcpHost = DeviceSimulationProfile.EnableLocalTcpHost ?? false;
			DeviceSimulationProfile.SendToServerViaUdp = DeviceSimulationProfile.SendToServerViaUdp ?? false;
			DeviceSimulationProfile.SendToAndroidViaAdb = DeviceSimulationProfile.SendToAndroidViaAdb ?? false;
			DeviceSimulationProfile.SendToAndroidViaUdp = DeviceSimulationProfile.SendToAndroidViaUdp ?? false;
			DeviceSimulationProfile.SendToIOSDevice = DeviceSimulationProfile.SendToIOSDevice ?? false;


			_applyAction = applyAction;
			_cancelAction = cancelAction;

			Fps = 12;

			ControlMethodItems = new ReadOnlyCollection<KeyValuePair<string, VirtualDrivingControlMethod>>(new List<KeyValuePair<string, VirtualDrivingControlMethod>>()
			{
				new KeyValuePair<string, VirtualDrivingControlMethod> ("Keyboard", VirtualDrivingControlMethod.Keyboard),
				new KeyValuePair<string, VirtualDrivingControlMethod> ("Gamepad", VirtualDrivingControlMethod.Gamepad),
			});
			SelectedControlMethodItem = ControlMethodItems.First();

			DrivingProfiles = GetPredefinedDrivingProfiles();
			SelectedDrivingProfile = DrivingProfiles.First();

			SendDrivingGpsEventToDeviceMockTarget = true;
			AutoSaveGpsEventsAfterDrivingComplete = true;
			AutoSaveGpsEventsDirectoryPath = DefaultAutoSaveGpsEventsDirectoryPath;
			if (!Directory.Exists(AutoSaveGpsEventsDirectoryPath))
			{
				Directory.CreateDirectory(AutoSaveGpsEventsDirectoryPath);
			}

			UsePredefinedRoute = false;
			RouteConductTypes = new ReadOnlyCollection<KeyValuePair<string, VirtualDrivingRouteConductType>>(new List<KeyValuePair<string, VirtualDrivingRouteConductType>>()
			{
				new KeyValuePair<string, VirtualDrivingRouteConductType> ("Display Only", VirtualDrivingRouteConductType.DisplayOnly),
				new KeyValuePair<string, VirtualDrivingRouteConductType> ("Full Conduct", VirtualDrivingRouteConductType.FullConduct),
			});
			SelectedRouteConductType = RouteConductTypes.First();
			DrivingRoutes = new ObservableCollection<VirtualDrivingRoute>(VirtualDrivingDataHelper.FindAllDrivingRoutesInLocalStorage());
			SelectedDrivingRoute = null;

			// Command initialization
			ApplyStartupSettingsCommand = new RelayCommand(ApplyStartupSettings);
			CancelStartupCommand = new RelayCommand(CancelStartup);
			OpenGamepadTestViewCommand = new RelayCommand(OpenGamepadTestView, () => true);
			ToggleSendGpsEventToDeviceSimulationTargetsCommand = new RelayCommand(ToggleSendGpsEventToDeviceSimulationTargets, () => true);
			BrowseForAutoSaveGpsEventsDirectoryCommand = new RelayCommand(BrowseForAutoSaveGpsEventsDirectory, () => true);
		}


		public int Fps
		{
			get => _fps;
			set => SetProperty(ref _fps, value, nameof(Fps));
		}

		public ReadOnlyCollection<KeyValuePair<string, VirtualDrivingControlMethod>> ControlMethodItems { get; }

		public KeyValuePair<string, VirtualDrivingControlMethod> SelectedControlMethodItem
		{
			get => _selectedControlMethodItem;
			set
			{
				SetProperty(ref _selectedControlMethodItem, value, nameof(SelectedControlMethodItem));
				OnPropertyChanged(nameof(GamepadTestButtonVisibility));
			}
		}

		public Visibility GamepadTestButtonVisibility
		{
			get
			{
				if (SelectedControlMethodItem.Value == VirtualDrivingControlMethod.Gamepad)
				{
					return Visibility.Visible;
				}
				else
				{
					return Visibility.Hidden;
				}
			}
		}

		public List<VirtualDrivingProfile> DrivingProfiles { get; }

		public VirtualDrivingProfile SelectedDrivingProfile
		{
			get => _selectedDrivingProfile;
			set
			{
				SetProperty(ref _selectedDrivingProfile, value, nameof(SelectedDrivingProfile));

				// Update values in custom driving profile properties
				if (SelectedDrivingProfile != null)
				{
					CustomAcceleration = SelectedDrivingProfile.Acceleration;
					CustomDeceleration = SelectedDrivingProfile.Deceleration;
					CustomMaxSpeed = SelectedDrivingProfile.MaxSpeed;
					CustomDragCoefficient = SelectedDrivingProfile.DragCoefficient;
					CustomMass = SelectedDrivingProfile.Mass;
					CustomDeltaAnglePerSecond = SelectedDrivingProfile.DeltaAnglePerSecond;
				}
			}
		}

		public bool UseCustomDrivingProfile
		{
			get => _useCustomDrivingProfile;
			set => SetProperty(ref _useCustomDrivingProfile, value, nameof(UseCustomDrivingProfile));
		}

		public decimal CustomAcceleration
		{
			get => _customAcceleration;
			set => SetProperty(ref _customAcceleration, value, nameof(CustomAcceleration));
		}

		public decimal CustomDeceleration
		{
			get => _customDeceleration;
			set => SetProperty(ref _customDeceleration, value, nameof(CustomDeceleration));
		}

		public decimal CustomMaxSpeed
		{
			get => _customMaxSpeed;
			set => SetProperty(ref _customMaxSpeed, value, nameof(CustomMaxSpeed));
		}

		public decimal CustomDragCoefficient
		{
			get => _customDragCoefficient;
			set => SetProperty(ref _customDragCoefficient, value, nameof(CustomDragCoefficient));
		}

		public decimal CustomDeltaAnglePerSecond
		{
			get => _customDeltaAnglePerSecond;
			set => SetProperty(ref _customDeltaAnglePerSecond, value, nameof(CustomDeltaAnglePerSecond));
		}

		public decimal CustomMass
		{
			get => _customMass;
			set => SetProperty(ref _customMass, value, nameof(CustomMass));
		}

		public GpsSimulationProfile DeviceSimulationProfile { get; private set; }

		public bool SendDrivingGpsEventToDeviceMockTarget
		{
			get => _sendDrivingGpsEventToDeviceMockTarget;
			set => SetProperty(ref _sendDrivingGpsEventToDeviceMockTarget, value, nameof(SendDrivingGpsEventToDeviceMockTarget));
		}

		public bool AutoSaveGpsEventsAfterDrivingComplete
		{
			get => _autoSaveGpsEventsAfterDrivingComplete;
			set => SetProperty(ref _autoSaveGpsEventsAfterDrivingComplete, value, nameof(AutoSaveGpsEventsAfterDrivingComplete));
		}

		public string AutoSaveGpsEventsDirectoryPath
		{
			get => _autoSaveGpsEventsDirectoryPath;
			set => SetProperty(ref _autoSaveGpsEventsDirectoryPath, value, nameof(AutoSaveGpsEventsDirectoryPath));
		}

		public bool UsePredefinedRoute
		{
			get => _usePredefinedRoute;
			set => SetProperty(ref _usePredefinedRoute, value, nameof(UsePredefinedRoute));
		}

		public ReadOnlyCollection<KeyValuePair<string, VirtualDrivingRouteConductType>> RouteConductTypes { get; }

		public KeyValuePair<string, VirtualDrivingRouteConductType> SelectedRouteConductType
		{
			get => _selectedRouteConductTypeItem;
			set => SetProperty(ref _selectedRouteConductTypeItem, value, nameof(SelectedRouteConductType));
		}

		public ObservableCollection<VirtualDrivingRoute> DrivingRoutes
		{
			get; private set;
		}

		public VirtualDrivingRoute SelectedDrivingRoute
		{
			get => _selectedDrivingRoute;
			set
			{
				SetProperty(ref _selectedDrivingRoute, value, nameof(SelectedDrivingRoute));
				OnPropertyChanged(nameof(RoutePreviewImageVisibility));
				OnPropertyChanged(nameof(RoutePreviewImageFilePath));
			}
		}

		public Visibility RoutePreviewImageVisibility
		{
			get
			{
				if (UsePredefinedRoute && SelectedDrivingRoute != null)
				{
					return Visibility.Visible;
				}
				else
				{
					return Visibility.Hidden;
				}
			}
		}

		public string RoutePreviewImageFilePath
		{
			get
			{
				if (SelectedDrivingRoute != null)
				{
					var routePreviewImageFilePath = VirtualDrivingDataHelper.GetRoutePreviewImageFilePath(SelectedDrivingRoute.Name);
					if (!string.IsNullOrEmpty(routePreviewImageFilePath))
					{
						return routePreviewImageFilePath;
					}
				}

				return "/Icons/ImageNotFound.png";
			}
		}

		public IRelayCommand OpenGamepadTestViewCommand { get; private set; }

		public IRelayCommand ApplyStartupSettingsCommand { get; private set; }

		public IRelayCommand CancelStartupCommand { get; private set; }

		public IRelayCommand ToggleSendGpsEventToDeviceSimulationTargetsCommand { get; private set;}

		public IRelayCommand BrowseForAutoSaveGpsEventsDirectoryCommand { get; private set; }

		public VirtualDrivingStartupSettings GetVirtualDrivingStartupSettings()
		{
			if (!SendDrivingGpsEventToDeviceMockTarget)
			{
				DeviceSimulationProfile.SendToVirtualSerialPort = false;
				DeviceSimulationProfile.EnableLocalTcpHost = false;
				DeviceSimulationProfile.SendToServerViaUdp = false;
				DeviceSimulationProfile.SendToAndroidViaAdb = false;
				DeviceSimulationProfile.SendToAndroidViaUdp = false;
				DeviceSimulationProfile.SendToIOSDevice = false;
			}

			var drivingProfile = SelectedDrivingProfile;
			if (UseCustomDrivingProfile)
			{
				drivingProfile = new VirtualDrivingProfile()
				{
					Name = "Custom",
					Acceleration = CustomAcceleration,
					Deceleration = CustomDeceleration,
					MaxSpeed = CustomMaxSpeed,
					DragCoefficient = CustomDragCoefficient,
					Mass = CustomMass,
					DeltaAnglePerSecond = CustomDeltaAnglePerSecond,
				};
			}

			var latestSettings = new VirtualDrivingStartupSettings()
			{
				GpsSimulationProfile = DeviceSimulationProfile,
				FramePerSecond = Fps,
				ControlMethod = SelectedControlMethodItem.Value,
				DrivingProfile = drivingProfile,
				SendDrivingGpsEventToDeviceMockTarget = SendDrivingGpsEventToDeviceMockTarget,
				AutoSaveGpsEventsAfterDrivingComplete = AutoSaveGpsEventsAfterDrivingComplete,
				AutoSaveGpsEventsDirectoryPath = AutoSaveGpsEventsDirectoryPath,
				UsePredefinedRoute = UsePredefinedRoute,
				RouteConductType = SelectedRouteConductType.Value,
				SelectedRoute = SelectedDrivingRoute,
			};

			return latestSettings;
		}

		private (bool, string?) ValidateStartupSettings()
		{
			bool isValid = true;
			var errorBuffer = new StringBuilder();

			if (Fps < 1 || Fps > 20)
			{
				isValid = false;
				errorBuffer.AppendLine("FPS must be integer between 1 and 20.");
			}

			if (SelectedControlMethodItem.Value == VirtualDrivingControlMethod.Gamepad)
			{
				// Check if there is Gamepad connectd now
				if (!XInputHelper.HasGamepadConnected())
				{
					isValid = false;
					errorBuffer.AppendLine("No Gamepad connected.");
				}
			}

			if (UseCustomDrivingProfile)
			{
				if (CustomAcceleration < 1 || CustomAcceleration > 5)
				{
					isValid = false;
					errorBuffer.AppendLine("Custom Acceleration must be decimal between 1 and 5.");
				}
				if (CustomDeceleration < 1 || CustomDeceleration > 100)
				{
					isValid = false;
					errorBuffer.AppendLine("Custom Deceleration must be decimal between 1 and 100.");
				}
				if (CustomMaxSpeed < 10 || CustomMaxSpeed > 50)
				{
					isValid = false;
					errorBuffer.AppendLine("Custom Max Speed must be decimal between 10 and 50.");
				}
				if (CustomDragCoefficient < 0 || CustomDragCoefficient > 2)
				{
					isValid = false;
					errorBuffer.AppendLine("Custom Drag Efficient must be decimal between 0 and 2.");
				}
				if (CustomDeltaAnglePerSecond < 5 || CustomDeltaAnglePerSecond > 45)
				{
					isValid = false;
					errorBuffer.AppendLine("Custom Delta Angle Per Second must be decimal between 5 and 45.");
				}
				if (CustomMass < 500 || CustomMass > 3000)
				{
					isValid = false;
					errorBuffer.AppendLine("Custom Mass must be decimal between 500kg and 3000kg.");
				}
			}

            if (AutoSaveGpsEventsAfterDrivingComplete)
            {
                // Check if AutoSaveGpsEventsDirectoryPath is valid
				if (string.IsNullOrWhiteSpace(AutoSaveGpsEventsDirectoryPath))
				{
					isValid = false;
					errorBuffer.AppendLine("Auto Save Gps Events Directory Path is empty.");
				}
				else if (!Directory.Exists(AutoSaveGpsEventsDirectoryPath))
				{
					isValid = false;
					errorBuffer.AppendLine("Auto Save Gps Events Directory Path is invalid.");
				}
            }

			if (UsePredefinedRoute)
			{
				if (SelectedDrivingRoute == null)
				{
					isValid = false;
					errorBuffer.AppendLine("Driving Route required.");
				}

				if (!VirtualDrivingDataHelper.RouteNameExistsInLocalStorage(SelectedDrivingRoute.Name))
				{
					isValid = false;
					errorBuffer.AppendLine($"Driving Route '{SelectedDrivingRoute.Name}' not found.");
				}
			}

            if (isValid)
			{
				return (true, null);
			}
			else
			{
				return (false, errorBuffer.ToString());
			}
		}

		private void ApplyStartupSettings()
		{
			var (isValid, errorMessage) = ValidateStartupSettings();
			if (!isValid)
			{
				MessageBox.Show(errorMessage, "Invalid Startup Settings", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			_applyAction?.Invoke();
		}

		private void CancelStartup()
		{
			_cancelAction?.Invoke();
		}

		private void OpenGamepadTestView()
		{
			var gamepadTestingDialog = new GamepadTestingDialog();
			var closeAction = () =>
			{
				gamepadTestingDialog.DialogResult = true;
				gamepadTestingDialog.Close();
			};

			using (var gamepadTestingVM = new GamepadTestingViewModel(Fps, closeAction))
			{
				gamepadTestingDialog.DataContext = gamepadTestingVM;
				gamepadTestingVM.StartGamepadInputLoop();

				gamepadTestingDialog.ShowDialog();
			}
		}

		private void ToggleSendGpsEventToDeviceSimulationTargets()
		{
			if (!SendDrivingGpsEventToDeviceMockTarget)
			{
				MessageBox.Show("No GPS Events will be sent to Device Simulation Targets after unchecking this option.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
			}
		}

		private void BrowseForAutoSaveGpsEventsDirectory()
		{
			var selectedDirectoryPath = DirectoryAndFileSelectionHelper.PromptForDirectorySelection(AutoSaveGpsEventsDirectoryPath);
			if (!string.IsNullOrWhiteSpace(selectedDirectoryPath)
					&& Directory.Exists(selectedDirectoryPath))
			{
				AutoSaveGpsEventsDirectoryPath = selectedDirectoryPath;
			}
		}

		private static List<VirtualDrivingProfile> GetPredefinedDrivingProfiles()
		{
			var profiles = new List<VirtualDrivingProfile>()
			{
				new VirtualDrivingProfile()
				{
					Name = "Default",
					Acceleration = 3.78m,
					Deceleration = 18.0m,
					MaxSpeed = 37.7777m, // mps
					DragCoefficient = 0.095m,
					Mass = 1200,	// kg
					DeltaAnglePerSecond = 15.0m, // degrees
				},
			};

			return profiles;
		}
	}
}
