using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [AddComponentMenu("Dexterity/Object Source Enum Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public class ObjectSourceEnumNode : BaseEnumStateNode
    {
        public UnityEngine.Object targetObject;
        [ObjectValue(objectFieldName: nameof(targetObject), fieldType: typeof(Enum))]
        public string targetProperty;

        private ObjectEnumContext objectCtx;
        public int targetEnumValue => objectCtx?.GetValue() ?? 0;
        public Type targetEnumType => objectCtx?.type;

        public void InitializeObjectContext() 
        {
            objectCtx = null;
            if (targetObject != null && !string.IsNullOrEmpty(targetProperty))
                objectCtx = new ObjectEnumContext(this, nameof(targetProperty));
        }

        protected override void Initialize()
        {
            InitializeObjectContext();
            base.Initialize();
        }

        protected override IEnumerable<(string enumOption, int enumValue)> GetEnumOptions()
        {
            if (targetEnumType == null)
                yield break;
            foreach (var enumOption in Enum.GetNames(targetEnumType)) 
            {
                yield return (enumOption, (int)Enum.Parse(targetEnumType, enumOption));
            }
        }
        public override int GetEnumValue() => Convert.ToInt32(targetEnumValue);
        
        protected override void UpdateInternal(bool ignoreDelays)
        {
            // since this type of node is using a data source, state should always be considered dirty
            stateDirty = true;
            
            base.UpdateInternal(ignoreDelays);
        }

        protected override void OnValidate() 
        {
            base.OnValidate();
            
            if (Application.isPlaying) 
                return;
            
            try 
            {
                // cache for sake of showing options in editor (enumToStateId.Keys)
                InitializeObjectContext();
            } catch (ArgumentException) 
            {
                // it's ok in editor!
            }
            CacheEnumOptions();
        }
    }
}
