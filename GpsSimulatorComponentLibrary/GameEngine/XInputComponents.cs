using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorComponentLibrary.GameEngine
{
	public class ValueChangeArgs<T> : EventArgs
	{
		public T Value { get; }

		public ValueChangeArgs(T value)
		{
			Value = value;
		}
	}

	public abstract class XInputComponent<T>
	{
		public event EventHandler<ValueChangeArgs<T>> ValueChanged;

		public T PreviousValue
		{
			get;
			private set;
		}

		private T value;
		public virtual T Value
		{
			get => value;
			internal set
			{
				PreviousValue = Value;
				if (!Value.Equals(value))
				{
					this.value = value;
					OnComponentChanged(new ValueChangeArgs<T>(Value));
				}
			}
		}

		public bool IsValueChanged

		{
			get => !Value.Equals(PreviousValue);
		}

		public XInputComponent(T initialValue)
		{
			value = initialValue;
		}

		protected virtual void OnComponentChanged(ValueChangeArgs<T> e)
		{
			ValueChanged?.Invoke(this, e);
		}
	}

	public class XInputThumbstick : XInputComponent<Vector2>
	{
		public bool Pressed { get; set; } = false;

		public float DeadZone { get; set; } = 0.0f;

		public override Vector2 Value
		{
			get => base.Value;
			internal set => base.Value = value.DeadZoneCorrected(DeadZone);
		}

		public XInputThumbstick(float deadZone = 0.0f, float initialX = 0.0f, float initialY = 0.0f) : base(new Vector2(initialX, initialY))
		{
			DeadZone = deadZone;
		}

		internal void SetValue(float x, float y)
		{
			Value = new Vector2(x, y);
		}
	}

	public class XInputGamepadStates
	{
		public bool IsConnected { get; set; } = false;

		public bool ButtonAPressed { get; set; } = false;

		public bool ButtonBPressed { get; set; } = false;

		public bool ButtonXPressed { get; set; } = false;

		public bool ButtonYPressed { get; set; } = false;

		public bool ButtonStartPressed { get; set; } = false;

		public bool ButtonBackPressed { get; set; } = false;

		public bool DPadUpPressed { get; set; } = false;

		public bool DPadDownPressed { get; set; } = false;

		public bool DPadLeftPressed { get; set; } = false;

		public bool DPadRightPressed { get; set; } = false;

		public bool LeftShoulderPressed { get; set; } = false;

		public bool RightShoulderPressed { get; set; } = false;

		public XInputThumbstick LeftThumbstick { get; set; } = new XInputThumbstick();

		public XInputThumbstick RightThumbstick { get; set; } = new XInputThumbstick();

		public void Reset()
		{
			ButtonAPressed = false;
			ButtonBPressed = false;
			ButtonXPressed = false;
			ButtonYPressed = false;
			ButtonStartPressed = false;
			ButtonBackPressed = false;
			DPadUpPressed = false;
			DPadDownPressed = false;
			DPadLeftPressed = false;
			DPadRightPressed = false;
			LeftShoulderPressed = false;
			RightShoulderPressed = false;
			LeftThumbstick.SetValue(0.0f, 0.0f);
			RightThumbstick.SetValue(0.0f, 0.0f);
		}
	
	}

}
