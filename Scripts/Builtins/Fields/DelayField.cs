using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
	[Preserve]
	public class DelayField : UpdateableField
	{
		[Tooltip("Delay when value changes from FALSE to TRUE")]
		public float delayTrue;
		[Tooltip("Delay when value changes from TRUE to FALSE")]
		public float delayFalse;
		
		[SerializeReference] public BaseField source;

		private int _lastValue;
		private float _timer;

		protected override void Initialize(FieldNode context) {
			base.Initialize(context);
			
			ClearUpstreamFields();
			AddUpstreamField(source);
		}

		public override void Update()
		{
			if (_lastValue > 0)
			{
				if (_timer > delayFalse)
				{
					_lastValue = source.value;
					_timer = 0;
				}
			}

			if (_lastValue == 0)
			{
				if (_timer > delayTrue)
				{
					_lastValue = source.value;
					_timer = 0;
				}
			}

			_timer += Time.unscaledDeltaTime;
			SetValue(_lastValue);
		}

		public override BaseField CreateDeepClone() {
			DelayField clone = (DelayField)base.CreateDeepClone();
			clone.source = source.CreateDeepClone();
			return clone;
		}
	}
}