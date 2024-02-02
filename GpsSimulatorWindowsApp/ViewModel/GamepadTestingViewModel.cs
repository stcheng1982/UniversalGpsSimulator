using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GpsSimulatorComponentLibrary.GameEngine;
using GpsSimulatorWindowsApp.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class GamepadTestingViewModel : ObservableObject, IDisposable
	{
		private readonly SolidColorBrush _buttonPressedBrush = new SolidColorBrush(Colors.Cyan);
		private readonly SolidColorBrush _buttonReleasedBrush = new SolidColorBrush(Colors.White);

		private Action _closeAction;
		private Task _gamepadInputLoopTask = null;
		private CancellationTokenSource _gamepadInputLoopCancelTokenSource = null;

		private bool _disposed = false;

		private string _gamepadUserIndex;
		private SolidColorBrush _buttonXBackgroundBrush;
		private SolidColorBrush _buttonYBackgroundBrush;
		private SolidColorBrush _buttonABackgroundBrush;
		private SolidColorBrush _buttonBBackgroundBrush;
		private SolidColorBrush _buttonStartBackgroundBrush;
		private SolidColorBrush _buttonBackBackgroundBrush;
		private SolidColorBrush _leftShoulderBackgroundBrush;
		private SolidColorBrush _rightShoulderBackgroundBrush;
		private SolidColorBrush _dpadUpBackgroundBrush;
		private SolidColorBrush _dpadDownBackgroundBrush;
		private SolidColorBrush _dpadLeftBackgroundBrush;
		private SolidColorBrush _dpadRightBackgroundBrush;

		private Thickness _leftThumbstickCircleMarginThickness = new Thickness(0, 0, 0, 0);
		private Thickness _rightThumbstickCircleMarginThickness = new Thickness(0, 0, 0, 0);


		public GamepadTestingViewModel(int fps, Action closeAction)
		{
			_closeAction = closeAction;
			TestingFps = fps;

			GamepadUserIndex = XInputHelper.GetGamepadUserIndex();

			CloseTestingViewCommand = new RelayCommand(CloseTestingView);
		}

		public int TestingFps { get; private set; }

		public string GamepadUserIndex
		{
			get => _gamepadUserIndex;
			set => SetProperty(ref _gamepadUserIndex, value, nameof(GamepadUserIndex));
		}

		public SolidColorBrush ButtonXBackgroundBrush
		{
			get => _buttonXBackgroundBrush;
			set => SetProperty(ref _buttonXBackgroundBrush, value, nameof(ButtonXBackgroundBrush));
		}

		public SolidColorBrush ButtonYBackgroundBrush
		{
			get => _buttonYBackgroundBrush;
			set => SetProperty(ref _buttonYBackgroundBrush, value, nameof(ButtonYBackgroundBrush));
		}

		public SolidColorBrush ButtonABackgroundBrush
		{
			get => _buttonABackgroundBrush;
			set => SetProperty(ref _buttonABackgroundBrush, value, nameof(ButtonABackgroundBrush));
		}

		public SolidColorBrush ButtonBBackgroundBrush
		{
			get => _buttonBBackgroundBrush;
			set => SetProperty(ref _buttonBBackgroundBrush, value, nameof(ButtonBBackgroundBrush));
		}

		public SolidColorBrush ButtonStartBackgroundBrush
		{
			get => _buttonStartBackgroundBrush;
			set => SetProperty(ref _buttonStartBackgroundBrush, value, nameof(ButtonStartBackgroundBrush));
		}

		public SolidColorBrush ButtonBackBackgroundBrush
		{
			get => _buttonBackBackgroundBrush;
			set => SetProperty(ref _buttonBackBackgroundBrush, value, nameof(ButtonBackBackgroundBrush));
		}

		public SolidColorBrush LeftShoulderBackgroundBrush
		{
			get => _leftShoulderBackgroundBrush;
			set => SetProperty(ref _leftShoulderBackgroundBrush, value, nameof(LeftShoulderBackgroundBrush));
		}

		public SolidColorBrush RightShoulderBackgroundBrush
		{
			get => _rightShoulderBackgroundBrush;
			set => SetProperty(ref _rightShoulderBackgroundBrush, value, nameof(RightShoulderBackgroundBrush));
		}

		public SolidColorBrush DpadUpBackgroundBrush
		{
			get => _dpadUpBackgroundBrush;
			set => SetProperty(ref _dpadUpBackgroundBrush, value, nameof(DpadUpBackgroundBrush));
		}

		public SolidColorBrush DpadDownBackgroundBrush
		{
			get => _dpadDownBackgroundBrush;
			set => SetProperty(ref _dpadDownBackgroundBrush, value, nameof(DpadDownBackgroundBrush));
		}

		public SolidColorBrush DpadLeftBackgroundBrush
		{
			get => _dpadLeftBackgroundBrush;
			set => SetProperty(ref _dpadLeftBackgroundBrush, value, nameof(DpadLeftBackgroundBrush));
		}

		public SolidColorBrush DpadRightBackgroundBrush
		{
			get => _dpadRightBackgroundBrush;
			set => SetProperty(ref _dpadRightBackgroundBrush, value, nameof(DpadRightBackgroundBrush));
		}
		
		public Thickness LeftThumbstickCircleMarginThickness
		{
			get => _leftThumbstickCircleMarginThickness;
			set => SetProperty(ref _leftThumbstickCircleMarginThickness, value, nameof(LeftThumbstickCircleMarginThickness));
		}

		public Thickness RightThumbstickCircleMarginThickness
		{
			get => _rightThumbstickCircleMarginThickness;
			set => SetProperty(ref _rightThumbstickCircleMarginThickness, value, nameof(RightThumbstickCircleMarginThickness));
		}


		public IRelayCommand CloseTestingViewCommand { get; private set; }

		public IRelayCommand InvokeGamepadButtonCommand { get; private set; }


		public void StartGamepadInputLoop()
		{
			_gamepadInputLoopCancelTokenSource = new CancellationTokenSource();
			_gamepadInputLoopTask = Task.Run(async () =>
			{
				await XInputHelper.RunGamepadInputLoopAsync(
					TestingFps,
					gamepadStates =>
					{
						HandleGamepadInputStates(gamepadStates);
					},
					_gamepadInputLoopCancelTokenSource.Token
					);
			});
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				if (_gamepadInputLoopTask != null)
				{
					_gamepadInputLoopCancelTokenSource.Cancel();
					_gamepadInputLoopTask.Wait(TimeSpan.FromSeconds(2));
					_gamepadInputLoopTask.Dispose();
					_gamepadInputLoopTask = null;
					_gamepadInputLoopCancelTokenSource?.Dispose();
				}

				_disposed = true;
			}
		}

		private void CloseTestingView()
		{
			_gamepadInputLoopCancelTokenSource?.Cancel();
			_closeAction?.Invoke();
		}

		private void HandleGamepadInputStates(XInputGamepadStates gamepadStates)
		{
			try
			{
				ButtonABackgroundBrush = gamepadStates.ButtonAPressed ? _buttonPressedBrush : _buttonReleasedBrush;
				ButtonBBackgroundBrush = gamepadStates.ButtonBPressed ? _buttonPressedBrush : _buttonReleasedBrush;
				ButtonXBackgroundBrush = gamepadStates.ButtonXPressed ? _buttonPressedBrush : _buttonReleasedBrush;
				ButtonYBackgroundBrush = gamepadStates.ButtonYPressed ? _buttonPressedBrush : _buttonReleasedBrush;
				ButtonStartBackgroundBrush = gamepadStates.ButtonStartPressed ? _buttonPressedBrush : _buttonReleasedBrush;
				ButtonBackBackgroundBrush = gamepadStates.ButtonBackPressed ? _buttonPressedBrush : _buttonReleasedBrush;
				LeftShoulderBackgroundBrush = gamepadStates.LeftShoulderPressed ? _buttonPressedBrush : _buttonReleasedBrush;
				RightShoulderBackgroundBrush = gamepadStates.RightShoulderPressed ? _buttonPressedBrush : _buttonReleasedBrush;
				DpadUpBackgroundBrush = gamepadStates.DPadUpPressed ? _buttonPressedBrush : _buttonReleasedBrush;
				DpadDownBackgroundBrush = gamepadStates.DPadDownPressed ? _buttonPressedBrush : _buttonReleasedBrush;
				DpadLeftBackgroundBrush = gamepadStates.DPadLeftPressed ? _buttonPressedBrush : _buttonReleasedBrush;
				DpadRightBackgroundBrush = gamepadStates.DPadRightPressed ? _buttonPressedBrush : _buttonReleasedBrush;

				var leftThumbValue = gamepadStates.LeftThumbstick.Value;
				//LeftThumbstickCircleMarginThickness = new Thickness(100.0 * leftThumbValue.X, -100.0 * leftThumbValue.Y, 0.0, 0.0);
				_leftThumbstickCircleMarginThickness.Left = 100.0 * leftThumbValue.X;
				_leftThumbstickCircleMarginThickness.Top = -100.0 * leftThumbValue.Y;
				_leftThumbstickCircleMarginThickness.Bottom = 0.0;
				_leftThumbstickCircleMarginThickness.Right = 0.0;
				OnPropertyChanged(nameof(LeftThumbstickCircleMarginThickness));

				var rightThumbValue = gamepadStates.RightThumbstick.Value;
				//RightThumbstickCircleMarginThickness = new Thickness(100.0 * rightThumbValue.X, -100.0 * rightThumbValue.Y, 0.0, 0.0);
				_rightThumbstickCircleMarginThickness.Left = 100.0 * rightThumbValue.X;
				_rightThumbstickCircleMarginThickness.Top = -100.0 * rightThumbValue.Y;
				_rightThumbstickCircleMarginThickness.Bottom = 0.0;
				_rightThumbstickCircleMarginThickness.Right = 0.0;
				OnPropertyChanged(nameof(RightThumbstickCircleMarginThickness));
			}
			catch (Exception ex)
			{
				LogHelper.Error($"HandleGamepadInputStates: {ex.Message}");
			}

		}

	}
}
