using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class ModifyNmeaSentenceOptionsViewModel : ObservableObject
	{
		private bool _gngnsEnabled;
		private bool _gpgllEnabled;
		private bool _gpggaEnabled;
		private bool _gprmcEnabled;
		private bool _gpvtgEnabled;

		private string? _selectedDeviceProfileName;

		private Action _applyAction;
		private Action _cancelAction;

		public List<string> DeviceProfileNames { get; } = new List<string>();

		public ModifyNmeaSentenceOptionsViewModel(
			NmeaSentencePlaybackOptions nmeaOptions,
			Action applyAction,
			Action cancelAction)
		{
			_gngnsEnabled = nmeaOptions.GNGNSEnabled;
			_gpgllEnabled = nmeaOptions.GPGLLEnabled;
			_gpggaEnabled = nmeaOptions.GPGGAEnabled;
			_gprmcEnabled = nmeaOptions.GPRMCEnabled;
			_gpvtgEnabled = nmeaOptions.GPVTGEnabled;
			_selectedDeviceProfileName = nmeaOptions.DeviceProfileName ?? NmeaDataHelper.CradleDeviceProfileName;

			_applyAction = applyAction;
			_cancelAction = cancelAction;

			ApplyRequestOptionsChangeCommand = new RelayCommand(ApplyRequestOptionsChange);
			CancelRequestOptionsChangeCommand = new RelayCommand(CancelRequestOptionsChange);

			DeviceProfileNames.AddRange(NmeaDataHelper.DeviceProfileNames);
		}

		public IRelayCommand ApplyRequestOptionsChangeCommand { get; private set; }

		public IRelayCommand CancelRequestOptionsChangeCommand { get; private set; }

		public bool GNGNSEnabled
		{
			get => _gngnsEnabled;
			set
			{
				_gngnsEnabled = value;
				OnPropertyChanged(nameof(GNGNSEnabled));
			}
		}

		public bool GPGLLEnabled
		{
			get => _gpgllEnabled;
			set
			{
				_gpgllEnabled = value;
				OnPropertyChanged(nameof(GPGLLEnabled));
			}
		}

		public bool GPGGAEnabled
		{
			get => _gpggaEnabled;
			set
			{
				_gpggaEnabled = value;
				OnPropertyChanged(nameof(GPGGAEnabled));
			}
		}

		public bool GPRMCEnabled
		{
			get => _gprmcEnabled;
			set
			{
				_gprmcEnabled = value;
				OnPropertyChanged(nameof(GPRMCEnabled));
			}
		}

		public bool GPVTGEnabled
		{
			get => _gpvtgEnabled;
			set
			{
				_gpvtgEnabled = value;
				OnPropertyChanged(nameof(GPVTGEnabled));
			}
		}

		public string SelectedDeviceProfileName
		{
			get => _selectedDeviceProfileName;
			set
			{
				_selectedDeviceProfileName = value;
				OnPropertyChanged(nameof(SelectedDeviceProfileName));
			}
		}

		public NmeaSentencePlaybackOptions GetLatestNmeaOptions()
		{
			var nmeaOptions = new NmeaSentencePlaybackOptions
			{
				GNGNSEnabled = GNGNSEnabled,
				GPGLLEnabled = GPGLLEnabled,
				GPGGAEnabled = GPGGAEnabled,
				GPRMCEnabled = GPRMCEnabled,
				GPVTGEnabled = GPVTGEnabled,
				DeviceProfileName =	SelectedDeviceProfileName
			};

			return nmeaOptions;
		}

		private void CancelRequestOptionsChange()
		{
			_cancelAction?.Invoke();
		}

		private void ApplyRequestOptionsChange()
		{
			_applyAction?.Invoke();
		}
	}
}
