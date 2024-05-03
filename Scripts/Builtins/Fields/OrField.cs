using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
	[Preserve]
	public class OrField : BaseField
	{
		[SerializeReference]
		public BaseField first;
		[SerializeReference]
		public BaseField second;

		public override BaseField CreateDeepClone()
		{
			OrField clone = (OrField)base.CreateDeepClone();
			clone.first = first.CreateDeepClone();
			clone.second = second.CreateDeepClone();
			return clone;
		}

		public override bool GetValue() {
			if (first != null && first.GetValue())
				return true;
			
			if (second != null && second.GetValue())
				return true;

			return false;
		}

		protected override void Initialize(FieldNode context) {
			base.Initialize(context);

			ClearUpstreamFields();

			AddUpstreamField(first);
			AddUpstreamField(second);
		}
	}
}
