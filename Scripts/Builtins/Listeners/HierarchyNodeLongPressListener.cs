using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// HierarchyNode-side counterpart of <see cref="FieldNodeLongPressListener"/>.
    /// Inherits from <see cref="HierarchyNodeClickListener"/> so it auto-finds
    /// providers in the surrounding HierarchyNode subtree.
    ///
    /// Timer state is delegated to a shared <see cref="LongPressTimer"/> instance
    /// (same one <see cref="FieldNodeLongPressListener"/> uses) so the tick math
    /// can't drift between the two variants.
    /// </summary>
    public class HierarchyNodeLongPressListener : HierarchyNodeClickListener
    {
        public float pressDuration = 1f;
        public float fillStartsAt = 0.05f;

        private readonly LongPressTimer _timer = new();

        [Preserve]
        public new bool IsPressed() => _timer.IsPressed;

        [Preserve] public float timeRemaining => _timer.TimeRemaining;

        protected override void Awake()
        {
            base.Awake();
            clickOnRelease = false;
            OnPressUp();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _timer.Release();
            onPressDown += OnPressDown;
            onPressUp += OnPressUp;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            onPressDown -= OnPressDown;
            onPressUp -= OnPressUp;
        }

        protected virtual void OnPressDown()
        {
            _timer.pressDuration = pressDuration;
            _timer.fillStartsAt = fillStartsAt;
            _timer.Press();
        }

        protected virtual void OnPressUp()
        {
            _timer.Release();
            UpdateProgress(0);
        }

        public void ManuallyTriggerPress() => OnPressDown();
        public void ManuallyReleasePress() => OnPressUp();

        private void Update()
        {
            var (completed, progress) = _timer.Tick(Time.unscaledDeltaTime);
            if (completed) OnWaitCompleted();
            else if (_timer.IsPressed) UpdateProgress(progress);
        }

        protected virtual void UpdateProgress(float progress) { }

        protected virtual void OnWaitCompleted() => TriggerClick();
    }
}
