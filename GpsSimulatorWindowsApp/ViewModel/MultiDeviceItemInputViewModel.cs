using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public abstract class MultiDeviceItemInputViewModel : ObservableObject, IDisposable
	{
		private LocalGpsDataFileType? _gpsDataFileType;
		private string _gpsDataFilePath;

		public MultiDeviceItemInputViewModel(GpsDataSourceViewModel parentVM)
		{
			ParentViewModel = parentVM;

			SelectGpsDataFileCommand = new AsyncRelayCommand<string>(
			async fileTypeName =>
			{
				var dataFileType = Enum.Parse<LocalGpsDataFileType>(fileTypeName);
				await ParentViewModel.SelectGpsDataFileForMultiDeviceItemAsync(this, dataFileType);
			},
			_ => true
			);

			RemoveSelfFromParentCommand = new RelayCommand(() =>
			{
				ParentViewModel.RemoveMultiDeviceInputItem(this);
			}, () => true);
		}


		public GpsDataSourceViewModel ParentViewModel { get; set; }

		public IAsyncRelayCommand<string> SelectGpsDataFileCommand { get; private set; }

		public IRelayCommand RemoveSelfFromParentCommand { get; private set; }

		public LocalGpsDataFileType? GpsDataFileType
		{
			get => _gpsDataFileType;
			set
			{
				SetProperty(ref _gpsDataFileType, value, nameof(GpsDataFileType));
				OnPropertyChanged(nameof(GpsDataFileTypeName));
			}
		}

		public string GpsDataFileTypeName
		{
			get
			{
				return _gpsDataFileType?.ToString() ?? "N/A";
			}
		}

		public string GpsDataFilePath
		{
			get => _gpsDataFilePath;
			set
			{
				SetProperty(ref _gpsDataFilePath, value, nameof(GpsDataFilePath));
			}
		}

		public virtual void Dispose()
		{
			if (ParentViewModel != null)
			{
				GpsDataFilePath = null;
				ParentViewModel = null;
			}
		}
	}
}
