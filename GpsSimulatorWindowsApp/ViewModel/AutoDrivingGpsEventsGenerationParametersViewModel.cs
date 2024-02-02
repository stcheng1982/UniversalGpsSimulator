using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GpsSimulatorWindowsApp.DataType;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class AutoDrivingGpsEventsGenerationParametersViewModel : ObservableObject
	{
		private decimal _acceleration;
		private decimal _deceleration;
		private decimal _maxSpeed;
		private decimal _turnSpeed;
		private int _maxAngleChangeInSegment;
		private int _gpsEventsIntervalInSeconds;

		private Action _applyAction;
		private Action _cancelAction;

		public AutoDrivingGpsEventsGenerationParametersViewModel(
			NaiveAutoDrivingPlanParameters parameters,
			Action applyAction,
			Action cancelAction)
		{
			if (parameters == null)
			{
				parameters = new NaiveAutoDrivingPlanParameters();
			}

			_acceleration = parameters.Acceleration;
			_deceleration = parameters.Deceleration;
			_maxSpeed = parameters.MaxSpeed;
			_turnSpeed = parameters.TurnSpeed;
			_maxAngleChangeInSegment = parameters.MaxAngleChangeInSegment;
			_gpsEventsIntervalInSeconds = parameters.GpsEventsIntervalInSeconds;

			_applyAction = applyAction;
			_cancelAction = cancelAction;

			ApplyParametersCommand = new RelayCommand(ApplyParameters);
			CancelParametersCommand = new RelayCommand(CancelParameters);
		}

		public decimal Acceleration
		{
			get => _acceleration;
			set => SetProperty(ref _acceleration, value, nameof(Acceleration));
		}

		public decimal Deceleration
		{
			get => _deceleration;
			set => SetProperty(ref _deceleration, value, nameof(Deceleration));
		}

		public decimal MaxSpeed
		{
			get => _maxSpeed;
			set => SetProperty(ref _maxSpeed, value, nameof(MaxSpeed));
		}

		public decimal TurnSpeed
		{
			get => _turnSpeed;
			set => SetProperty(ref _turnSpeed, value, nameof(TurnSpeed));
		}

		public int MaxAngleChangeInSegment
		{
			get => _maxAngleChangeInSegment;
			set => SetProperty(ref _maxAngleChangeInSegment, value, nameof(MaxAngleChangeInSegment));
		}

		public int GpsEventsIntervalInSeconds
		{
			get => _gpsEventsIntervalInSeconds;
			set => SetProperty(ref _gpsEventsIntervalInSeconds, value, nameof(GpsEventsIntervalInSeconds));
		}

		public IRelayCommand ApplyParametersCommand { get; private set; }

		public IRelayCommand CancelParametersCommand { get; private set; }

		public NaiveAutoDrivingPlanParameters GetAppliedDrivingPLanParameters()
		{
			return new NaiveAutoDrivingPlanParameters()
			{
				Acceleration = Acceleration,
				Deceleration = Deceleration,
				MaxSpeed = MaxSpeed,
				TurnSpeed = TurnSpeed,
				MaxAngleChangeInSegment = MaxAngleChangeInSegment,
				GpsEventsIntervalInSeconds = GpsEventsIntervalInSeconds
			};
		}

		private string? ValidateInput()
		{
			bool isValid = true;
			var errorBuffer = new StringBuilder();
			if (Acceleration < 1)
			{
				errorBuffer.AppendLine("Acceleration must be >= 1");
				isValid = false;
			}

			if (Deceleration < 1)
			{
				errorBuffer.AppendLine("Deceleration must be >= 1");
				isValid = false;
			}

			if (MaxSpeed < 10)
			{
				errorBuffer.AppendLine("MaxSpeed must be >= 10");
				isValid = false;
			}

			if (TurnSpeed <= 0)
			{
				errorBuffer.AppendLine("TurnSpeed must be > 0");
				isValid = false;
			}

			if (MaxAngleChangeInSegment <= 0 || MaxAngleChangeInSegment > 30)
			{
				errorBuffer.AppendLine("MaxAngleChangeInSegment must be > 0 and <= 30");
				isValid = false;
			}

			if (!isValid)
			{
				return errorBuffer.ToString();
			}
			else
			{
				return null;
			}
		}


		private void ApplyParameters()
		{
			var errorMessage = ValidateInput();
			if (!string.IsNullOrEmpty(errorMessage))
			{
				MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			_applyAction?.Invoke();
		}

		private void CancelParameters()
		{
			_cancelAction?.Invoke();
		}
	}
}
