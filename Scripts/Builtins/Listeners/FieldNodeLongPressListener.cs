using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// FieldNode-side long-press listener. Subclasses <see cref="FieldNodeClickListener"/>
    /// so callers can still treat it as a click listener (access <c>onClick</c>,
    /// auto-find via <c>GetComponent&lt;FieldNodeClickListener&gt;</c>, etc.).
    ///
    /// Timer state is delegated to a shared <see cref="LongPressTimer"/> instance
    /// so this class and <see cref="HierarchyNodeLongPressListener"/> can't drift —
    /// only the wiring around the base class differs.
    ///
    /// The normal "release fires onClick" behaviour is suppressed via
    /// <see cref="BaseClickListener.clickOnRelease"/>; onClick fires only when the
    /// timer completes.
    /// </summary>
    public class FieldNodeLongPressListener : FieldNodeClickListener
    {
        public float pressDuration = 1f;
        public float fillStartsAt = 0.05f;

        private readonly LongPressTimer _timer = new();

        // Hides BaseClickListener.IsPressed (different semantics — "has the press
        // lasted long enough to count as a long-press" vs "is the press input active").
        [Preserve]
        public new bool IsPressed() => _timer.IsPressed;

        [Preserve] public float timeRemaining => _timer.TimeRemaining;

        protected override void Awake()
        {
            base.Awake();
            clickOnRelease = false;   // timer owns the click trigger.
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
