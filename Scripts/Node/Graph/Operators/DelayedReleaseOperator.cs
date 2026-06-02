using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Debounces its combined input with independent turn-on and turn-off delays.
    ///
    /// The input is the OR of every connected source (active if any input is active) — for the
    /// usual single-input wiring that's just that input. The output then follows the input
    /// through two configurable delays:
    /// <list type="bullet">
    ///   <item><b>Delayed on</b> (<see cref="onDelay"/>): the input must stay active continuously
    ///         for this long before the output turns on. Shorter blips are filtered.</item>
    ///   <item><b>Delayed off / auto-release</b> (<see cref="offDelay"/>): once on, the output lingers
    ///         this long after the input goes inactive before it releases. Shorter drops are held
    ///         through.</item>
    /// </list>
    /// Either delay of 0 makes that edge instant.
    ///
    /// The delay clock runs in <see cref="Update"/> (runtime only). At edit time there's no clock,
    /// so the output mirrors the undelayed input — drive previews with the per-source override pills.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph/Delayed Release Operator")]
    public class DelayedReleaseOperator : GraphOperator
    {
        [SerializeField, Tooltip("Seconds the input must stay active before the output turns on. 0 = instant.")]
        private float onDelay = 0f;

        [SerializeField, Tooltip("Seconds the output lingers after the input goes inactive before it releases (auto-release). 0 = instant.")]
        private float offDelay = 0.5f;

        // Raw combined input, refreshed every evaluation pass by ComputeOutput. Upstream changes are
        // event-driven (they re-evaluate the node, which re-runs ComputeOutput), so between passes this
        // stays valid — letting Update run the delay clock against a stable value.
        [System.NonSerialized] private bool _rawInput;
        [System.NonSerialized] private bool _output;
        [System.NonSerialized] private float _timer;

        protected override bool ComputeOutput(IReadOnlyList<bool> inputs)
        {
            bool raw = AnyActive(inputs);
            _rawInput = raw;

            // No clock at edit time — show the undelayed signal so the graph preview stays meaningful.
            if (!Application.isPlaying)
            {
                _output = raw;
                _timer = 0f;
                return _output;
            }

            // Instant edge when the relevant delay is zero, so response lands within this same pass
            // instead of waiting a frame for Update.
            float delay = raw ? onDelay : offDelay;
            if (raw != _output && delay <= 0f)
            {
                _output = raw;
                _timer = 0f;
            }
            return _output;
        }

        private void Update()
        {
            if (_rawInput == _output)
            {
                _timer = 0f;
                return;
            }

            if (!Manager.isAlive) return;   // no Dexterity clock running yet

            // Advance on Dexterity's own clock (unscaled * Database.timeScale), like every other
            // transition — not Time.deltaTime — so time-scaling and previews behave consistently.
            _timer += (float)Database.instance.deltaTime;
            float delay = _rawInput ? onDelay : offDelay;
            if (_timer >= delay)
            {
                _output = _rawInput;
                _timer = 0f;
                // Propagate: re-evaluates the node, whose ComputeOutput call returns the new latched output.
                NotifyExternalChanged();
            }
        }

        private static bool AnyActive(IReadOnlyList<bool> inputs)
        {
            for (var i = 0; i < inputs.Count; i++)
                if (inputs[i]) return true;
            return false;
        }
    }
}
