using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GpsSimulatorComponentLibrary.GameEngine
{
	public struct DirectInputKeyState
	{
		public int Timestamp { get; set; }

		public int Sequence { get; set; }

		public string Key { get; set; }

		public bool IsPressed { get; set; }

		public bool IsReleased  => !IsPressed;
	}

	public static class DirectInputHelper
	{
		public static readonly HashSet<Key> CommonKeyset = new HashSet<Key>()
		{
			Key.Space, Key.Return, Key.LeftControl, Key.RightControl, Key.LeftShift, Key.RightShift,
			Key.N, Key.P, Key.S,
		};

		public static readonly HashSet<Key> DrivingControlKeyset = new HashSet<Key>()
		{
			Key.Left, Key.Right, Key.Up, Key.Down, // Direction
			Key.A, Key.B, Key.D,	// Acc & Deacc
		};

		public static HashSet<Key> GetKeysetForVirtualDriving(bool commonKeysOnly)
		{
			if (commonKeysOnly)
			{
				return CommonKeyset;
			}
			else
			{
				return CommonKeyset.Union(DrivingControlKeyset).ToHashSet();
			}
		}

		public static async Task RunKeyboardInputLoopAsync(
			int fps,
			HashSet<Key> interestedKeys,
			Action<DirectInputKeyState[]> stateUpdateAction,
			CancellationToken cancellationToken)
		{
			if (interestedKeys == null)
			{
				throw new ArgumentNullException(nameof(interestedKeys));
			}

			if (fps <= 0 || fps >  GameEngineConstants.MaxFramePerSecond)
			{
				throw new ArgumentOutOfRangeException(nameof(fps));
			}

			if (stateUpdateAction == null)
			{
				throw new ArgumentNullException(nameof(stateUpdateAction));
			}

			if (cancellationToken == null)
			{
				throw new ArgumentNullException(nameof(cancellationToken));
			}

			long frameTimeInMS = 1000 / fps;
			var timeWatch = new Stopwatch();
			timeWatch.Start();
			try
			{
				using (var directInput = new DirectInput())
				{
					using (var keyboard = new Keyboard(directInput))
					{
						keyboard.Properties.BufferSize = 128;
						keyboard.Acquire();

						// Poll events from Keyboard
						while (!cancellationToken.IsCancellationRequested)
						{
							var msSinceLastFrame = timeWatch.ElapsedMilliseconds;
							if (msSinceLastFrame + 5 < frameTimeInMS)
							{
								await Task.Delay((int)(frameTimeInMS - msSinceLastFrame));
							}

							keyboard.Poll();
							var keyStates = keyboard.GetBufferedData();
							if (interestedKeys.Any())
							{
								keyStates = keyStates.Where(x => interestedKeys.Contains(x.Key)).ToArray();
							}

							if (keyStates?.Any() == true)
							{
								var outputKeyStates = keyStates.Select(rawKeyState => new DirectInputKeyState()
								{
									Timestamp = rawKeyState.Timestamp,
									Sequence = rawKeyState.Sequence,
									Key = rawKeyState.Key.ToString(),
									IsPressed = rawKeyState.IsPressed,
								}).ToArray();
								stateUpdateAction(outputKeyStates);
							}

							timeWatch.Restart();
						}

						keyboard.Unacquire();
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
			finally
			{
				timeWatch.Stop();
			}
		}

		/// <summary>
		/// Leave as for reference, we will use XInput as preferred means for Gamepad input
		/// </summary>
		/// <param name="directInput"></param>
		/// <param name="cancelTokenSource"></param>
		public static void RunJoystickInputLoop(DirectInput directInput, CancellationToken cancelTokenSource)
		{
			// Find a Joystick Guid
			var gamepadGuid = Guid.Empty;

			foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
			{
				gamepadGuid = deviceInstance.InstanceGuid;
				if (gamepadGuid != Guid.Empty)
				{
					break;
				}
			}

			//// If Gamepad not found, look for a Joystick
			//if (joystickGuid == Guid.Empty)
			//	foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
			//		joystickGuid = deviceInstance.InstanceGuid;

			// If Joystick not found, throws an error
			if (gamepadGuid == Guid.Empty)
			{
				MessageBox.Show("GamePad not found!");
				return;
			}

			// Instantiate the joystick
			using (var joystick = new Joystick(directInput, gamepadGuid))
			{

				Console.WriteLine("Found Joystick/Gamepad with GUID: {0}", gamepadGuid);

				// Query all suported ForceFeedback effects
				var allEffects = joystick.GetEffects();
				foreach (var effectInfo in allEffects)
				{
					Console.WriteLine("Effect available {0}", effectInfo.Name);
				}

				var buttonCount = joystick.Capabilities.ButtonCount;
				var buttonObjects = joystick.GetObjects(DeviceObjectTypeFlags.Button);
				foreach (var buttonObject in buttonObjects)
				{
					Debug.WriteLine($"[Button] Id: {buttonObject.ObjectId}, Name: {buttonObject.Name}, Aspect: {buttonObject.Aspect}, Dimension: {buttonObject.Dimension}");
				}

				// Set BufferSize in order to use buffered data.
				joystick.Properties.BufferSize = 128;

				// Acquire the joystick
				joystick.Acquire();

				// Poll events from joystick
				while (!cancelTokenSource.IsCancellationRequested)
				{
					joystick.Poll();
					var datas = joystick.GetBufferedData();
					foreach (var state in datas)
					{
						Debug.WriteLine(state);
					}
				}
			}

		}

	}
}
