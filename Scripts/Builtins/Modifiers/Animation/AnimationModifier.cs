using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Builtins
{
    public class AnimationModifier : ComponentModifier<Animator>, ISupportPropertyFreeze
    {
        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public string stateName;
            public float timestamp;
        }

        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;

            float targetTimestamp = 0f;
            string activeStateName = null;

            foreach (var kv in transitionState.keyValuePairs)
            {
                var property = (Property)GetProperty(kv.Key);
                var value = kv.Value;

                if (value > 0f && !string.IsNullOrEmpty(property.stateName))
                {
                    activeStateName = property.stateName;
                }

                targetTimestamp += Mathf.Lerp(0, property.timestamp, value);
            }

            if (component == null || string.IsNullOrEmpty(activeStateName))
                return;

            // Check if animator has a runtime controller
            if (component.runtimeAnimatorController == null)
            {
                Debug.LogWarning("AnimationModifier: Animator has no RuntimeAnimatorController", this);
                return;
            }

            // Get the animation clip info to calculate normalized time
            var clips = component.runtimeAnimatorController.animationClips;

            float clipLength = 1f;
            foreach (var clip in clips)
            {
                // Match by clip name instead of state info
                if (clip.name == activeStateName || activeStateName.Contains(clip.name))
                {
                    clipLength = clip.length;
                    break;
                }
            }

            // Convert timestamp to normalized time
            var currentNormalizedTime = clipLength > 0 ? targetTimestamp / clipLength : 0f;

            // Play the animation at the specific timestamp
            component.Play(activeStateName, 0, currentNormalizedTime);
            component.speed = 0f; // Pause the animation so it stays at this timestamp

#if UNITY_EDITOR
            // Force animator to update in edit mode so the animation seeks immediately
            if (!Application.isPlaying)
            {
                var aw = UnityEditor.EditorWindow.GetWindow<UnityEditor.AnimationWindow>();
                if (aw != null)
                    aw.time = targetTimestamp;
            }
#endif
        }

        public void FreezeProperty(PropertyBase property)
        {
#if UNITY_EDITOR
            if (component == null)
                return;

            var aw = UnityEditor.EditorWindow.GetWindow<UnityEditor.AnimationWindow>();
            if (aw == null)
            {
                Debug.LogError("AnimationModifier: Could not find Animation Window. Please open it to freeze the property.", this);
                return;
            }

            var prop = (Property)property;

            var clips = component.runtimeAnimatorController.animationClips;
            foreach (var clip in clips)
            {
                if (string.IsNullOrEmpty(prop.stateName))
                    prop.stateName = clip.name;
                
                prop.timestamp = aw.time;
                break;
            }
#endif
        }
    }
}
