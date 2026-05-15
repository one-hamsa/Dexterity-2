namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// Shared timer state for long-press listeners. Non-MonoBehaviour so both
    /// <see cref="FieldNodeLongPressListener"/> and
    /// <see cref="HierarchyNodeLongPressListener"/> can compose it without
    /// duplicating the tick math.
    ///
    /// Owners drive the lifecycle: call <see cref="Press"/> / <see cref="Release"/>
    /// from press-down / press-up handlers and <see cref="Tick"/> from Update,
    /// then react to the returned <c>completed</c> and <c>progress</c>.
    /// </summary>
    public class LongPressTimer
    {
        /// <summary>Seconds the press must be held before <see cref="Tick"/> reports completed.</summary>
        public float pressDuration = 1f;

        /// <summary>0–1 fraction of <see cref="pressDuration"/> the timer starts at on Press
        /// (a small head-start so the first frame doesn't read as 0% progress).</summary>
        public float fillStartsAt = 0.05f;

        /// <summary>True between Press and either Release or completion.</summary>
        public bool IsPressed { get; private set; }

        public float TimeRemaining => pressDuration - _elapsed;

        private float _elapsed;

        public void Press()
        {
            IsPressed = true;
            _elapsed = fillStartsAt * pressDuration;
        }

        public void Release()
        {
            IsPressed = false;
        }

        /// <summary>
        /// Advance the timer by <paramref name="dt"/> seconds.
        /// Returns whether the timer just reached <see cref="pressDuration"/>
        /// this tick, plus the current progress in [0, 1].
        /// </summary>
        public (bool completed, float progress) Tick(float dt)
        {
            if (!IsPressed) return (false, 0f);

            _elapsed += dt;
            if (_elapsed >= pressDuration)
            {
                IsPressed = false;
                return (true, 1f);
            }
            return (false, _elapsed / pressDuration);
        }
    }
}
