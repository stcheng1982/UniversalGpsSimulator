using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GpsSimulatorComponentLibrary.GameEngine
{
	public static class XInputHelper
	{
		public static bool HasGamepadConnected()
		{
			var controller = new Controller(UserIndex.One);
			if (!controller.IsConnected)
			{
				return false;
			}

			return true;
		}

		public static string GetGamepadUserIndex()
		{
			var controller = new Controller(UserIndex.One);
			if (!controller.IsConnected)
			{
				return "N/A";
			}
			
			return controller.UserIndex.ToString();
		}

		public static async Task RunGamepadInputLoopAsync(
			int fps,
			Action<XInputGamepadStates> stateUpdateAction,
			CancellationToken cancellationToken)
		{
			if (fps <= 0 || fps > GameEngineConstants.MaxFramePerSecond)
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
				var controller = new Controller(UserIndex.One);
				var gamepadInputStates = new XInputGamepadStates();

				// Start the gamepad input loop
				while (!cancellationToken.IsCancellationRequested)
				{
					var msSinceLastFrame = timeWatch.ElapsedMilliseconds;
					if (msSinceLastFrame + 5 < frameTimeInMS)
					{
						await Task.Delay((int)(frameTimeInMS - msSinceLastFrame));
					}

					gamepadInputStates.IsConnected = controller.IsConnected;
					if (!gamepadInputStates.IsConnected)
					{
						gamepadInputStates.Reset();
						continue;
					}

					var gamepad = controller.GetState().Gamepad;

					gamepadInputStates.ButtonAPressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.A);
					gamepadInputStates.ButtonBPressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.B);
					gamepadInputStates.ButtonXPressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.X);
					gamepadInputStates.ButtonYPressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.Y); 
					gamepadInputStates.ButtonStartPressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.Start);
					gamepadInputStates.ButtonBackPressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.Back);
					gamepadInputStates.LeftShoulderPressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
					gamepadInputStates.RightShoulderPressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);

					gamepadInputStates.DPadUpPressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp);
					gamepadInputStates.DPadDownPressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown);
					gamepadInputStates.DPadLeftPressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft);
					gamepadInputStates.DPadRightPressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight);

					gamepadInputStates.LeftThumbstick.Pressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
					gamepadInputStates.LeftThumbstick.SetValue(gamepad.LeftThumbX.RemapF(short.MinValue, short.MaxValue, XInputConstants.MinThumb, XInputConstants.MaxThumb),
									   gamepad.LeftThumbY.RemapF(short.MinValue, short.MaxValue, XInputConstants.MinThumb, XInputConstants.MaxThumb));

					gamepadInputStates.RightThumbstick.Pressed = gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);
					gamepadInputStates.RightThumbstick.SetValue(gamepad.RightThumbX.RemapF(short.MinValue, short.MaxValue, XInputConstants.MinThumb, XInputConstants.MaxThumb),
									   gamepad.RightThumbY.RemapF(short.MinValue, short.MaxValue, XInputConstants.MinThumb, XInputConstants.MaxThumb));
					//Debug.WriteLine($"LeftThumbX: {leftThumbX}, LeftThumbY: {leftThumbY},ButtonA: {buttonAFlag}, ButtonB: {buttonBFlag}, ButtonStart: {buttonStartFlag}");

					stateUpdateAction?.Invoke(gamepadInputStates);

					timeWatch.Restart();
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

	}
}
