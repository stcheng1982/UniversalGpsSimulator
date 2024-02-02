using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Dialogs;
using GpsSimulatorWindowsApp.Helpers;
using System;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class PatrolfinderServerUdpDeviceInputViewModel : MultiDeviceItemInputViewModel
	{
		private string? _deviceId;

		public string? DeviceId
		{
			get => _deviceId;
			set
			{
				SetProperty(ref _deviceId, value, nameof(DeviceId));
			}
		}

		public NmeaSentencePlaybackOptions? NmeaOptions { get; set; }

		public IRelayCommand ConfigureItemNmeaOptionsCommand { get; private set; }

		public PatrolfinderServerUdpDeviceInputViewModel(GpsDataSourceViewModel parentVM) : base(parentVM)
		{
			// Init Commands
			ConfigureItemNmeaOptionsCommand = new RelayCommand(() =>
			{
				ConfigureNmeaSentenceOptions();
			}, () => true);
		}

		public void ConfigureNmeaSentenceOptions()
		{
			var currentNmeaOptions = NmeaOptions ?? NmeaSentencePlaybackOptions.Default;
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
				var modifyNmeaOptionsVM = (modifyNmeaOptionsDialog.DataContext as ModifyNmeaSentenceOptionsViewModel);
				NmeaOptions = modifyNmeaOptionsVM.GetLatestNmeaOptions();
			}

		}
	}
}
