using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorComponentLibrary.GameEngine
{
	public static class RemapExtension
	{
		public static float RemapF(this float value, float inMin, float inMax, float outMin, float outMax)
		{
			return Math.Min(outMax, Math.Max(outMin, (value - inMin) * (outMax - outMin) / (inMax - inMin) + outMin));
		}

		public static float RemapF(this byte value, float inMin, float inMax, float outMin, float outMax)
		{
			return ((float)value).RemapF(inMin, inMax, outMin, outMax);
		}

		public static float RemapF(this short value, float inMin, float inMax, float outMin, float outMax)
		{
			return ((float)value).RemapF(inMin, inMax, outMin, outMax);
		}

		public static float RemapF(this int value, float inMin, float inMax, float outMin, float outMax)
		{
			return ((float)value).RemapF(inMin, inMax, outMin, outMax);
		}
	}

	public static class DeadZoneCorrectedExtension
	{
		public static float DeadZoneCorrected(this float value, float deadZone)
		{
			return (Math.Abs(value) > deadZone) ? value : 0.0f;
		}

		public static Vector2 DeadZoneCorrected(this Vector2 vector, float deadZone)
		{
			return new Vector2(vector.X.DeadZoneCorrected(deadZone), vector.Y.DeadZoneCorrected(deadZone));
		}
	}

	public static class XInputGamepadStatesExtension
	{
		public static string ToJsonObjectString(this XInputGamepadStates states)
		{
			var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ssZ");
			return $@"{{
""time"": ""{timestamp}"",
""gamepad"": {{

	""ButtonAPressed"": {states.ButtonAPressed.ToString().ToLower()},
	""ButtonBPressed"": {states.ButtonBPressed.ToString().ToLower()},
	""ButtonXPressed"": {states.ButtonXPressed.ToString().ToLower()},
	""ButtonYPressed"": {states.ButtonYPressed.ToString().ToLower()},
	""ButtonStartPressed"": {states.ButtonStartPressed.ToString().ToLower()},
	""ButtonBackPressed"": {states.ButtonBackPressed.ToString().ToLower()},
	""LeftShoulderPressed"": {states.LeftShoulderPressed.ToString().ToLower()},
	""RightShoulderPressed"": {states.RightShoulderPressed.ToString().ToLower()},
	""DPadUpPressed"": {states.DPadUpPressed.ToString().ToLower()},
	""DPadDownPressed"": {states.DPadDownPressed.ToString().ToLower()},
	""DPadLeftPressed"": {states.DPadLeftPressed.ToString().ToLower()},
	""DPadRightPressed"": {states.DPadRightPressed.ToString().ToLower()},

	""LeftThumbstick"": {{
		""Pressed"": {states.LeftThumbstick.Pressed.ToString().ToLower()},
		""X"": {states.LeftThumbstick.Value.X},
		""Y"": {states.LeftThumbstick.Value.Y}
	}},
	""RightThumbstick"": {{
		""Pressed"": {states.RightThumbstick.Pressed.ToString().ToLower()},
		""X"": {states.RightThumbstick.Value.X},
		""Y"": {states.RightThumbstick.Value.Y}
	}},
}}
}}
";
				
		}
	}
}
