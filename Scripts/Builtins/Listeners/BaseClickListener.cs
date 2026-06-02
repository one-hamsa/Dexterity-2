using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// Shared base for click-style listeners. Owns the events, the press-down/up
    /// diff loop, the "click = released while hovering, suppressed when disabled
    /// or hidden" rule, and the WasPressedThisFrame/GetTimeSinceClick API.
    ///
    /// Subclasses provide the input plumbing: how to read the four boolean
    /// signals (<see cref="IsPressed"/>, <see cref="IsHover"/>,
    /// <see cref="IsDisabled"/>, <see cref="IsHidden"/>) and when to call
    /// <see cref="OnPressMayHaveChanged"/> as the press signal flips.
    ///
    /// Concrete implementations:
    /// - <see cref="FieldNodeClickListener"/> — subscribes to FieldNode OutputFields.
    /// - <see cref="GraphNodeClickListener"/> — subscribes to GraphSource events.
    /// </summary>
    public abstract class BaseClickListener : MonoBehaviour
    {
        [SerializeField]
        public UnityEvent onClick;

        [Tooltip("Fire onClick automatically when the press is released while hovering. " +
                 "Turn off when a sibling component drives click timing (e.g. FieldNodeLongPressListener).")]
        public bool clickOnRelease = true;

        public event Action onPressDown;
        public event Action onPressUp;

        private bool _lastPressed;
        private int _pressFrame = -1;
        private double _pressTime = double.MinValue;

        [Preserve]
        public bool WasPressedThisFrame() => _pressFrame == Time.frameCount - 1;

        public double GetTimeSinceClick() => Time.realtimeSinceStartupAsDouble - _pressTime;

        /// <summary>Subclass hook: is the press input currently active?</summary>
        protected abstract bool IsPressed();

        /// <summary>Subclass hook: is the hover input currently active?</summary>
        protected abstract bool IsHover();

        /// <summary>
        /// Subclass hook: is the listener gated off by a "disabled" signal?
        /// Return false if the subclass doesn't model disabled state.
        /// </summary>
        protected abstract bool IsDisabled();

        /// <summary>
        /// Subclass hook: is the listener gated off by a "hidden / not visible" signal?
        /// Return false if the subclass doesn't model hidden state.
        /// </summary>
        protected abstract bool IsHidden();

        /// <summary>
        /// Subclasses call this when the press signal MIGHT have changed.
        /// Fires <see cref="onPressDown"/>/<see cref="onPressUp"/>/<see cref="onClick"/> as appropriate;
        /// suppressed entirely while disabled or hidden.
        /// </summary>
        protected void OnPressMayHaveChanged()
        {
            var now = IsPressed();
            if (now == _lastPressed) return;
            _lastPressed = now;

            // Suppress press semantics while hidden or disabled. The diff is
            // still recorded above so re-entering interactive state doesn't
            // fire a spurious onPressUp for a press that started while gated.
            if (IsHidden()) return;
            if (IsDisabled()) return;

            if (now)
            {
                onPressDown?.Invoke();
                return;
            }

            onPressUp?.Invoke();

            // Click = released while still hovering. clickOnRelease lets external
            // sibling components (e.g. FieldNodeLongPressListener) own click timing.
            if (clickOnRelease && IsHover())
                OnPressComplete();
        }

        protected virtual void OnPressComplete() => TriggerClick();

        public void TriggerClick()
        {
            _pressFrame = Time.frameCount;
            _pressTime = Time.realtimeSinceStartupAsDouble;
            onClick?.Invoke();
        }
    }
}
