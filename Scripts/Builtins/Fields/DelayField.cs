using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
	[Preserve]
	public class DelayField : BaseField
	{
		[Tooltip("Delay when value changes from FALSE to TRUE")]
		public float delayTrue;
		[Tooltip("Delay when value changes from TRUE to FALSE")]
		public float delayFalse;
		
		[SerializeReference] public BaseField source;

		private bool _lastValue;
		private float _timer;
		
		public override bool GetValue() {
			if (_lastValue) {
				if (_timer > delayFalse) {
					_lastValue = source.GetValue();
					_timer = 0;
				}
			}
			
			if (!_lastValue) {
				if (_timer > delayTrue) {
					_lastValue = source.GetValue();
					_timer = 0;
				}
			}
			
			_timer += Time.unscaledDeltaTime;
			return _lastValue;
		}

		protected override void Initialize(FieldNode context) {
			base.Initialize(context);
			
			ClearUpstreamFields();
			AddUpstreamField(source);
		}

		public override BaseField CreateDeepClone() {
			DelayField clone = (DelayField)base.CreateDeepClone();
			clone.source = source.CreateDeepClone();
			return clone;
		}
	}
}