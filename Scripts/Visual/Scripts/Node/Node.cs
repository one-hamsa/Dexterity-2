using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [AddComponentMenu("Dexterity/Visual/Dexterity Visual - Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public partial class Node : MonoBehaviour
    {
        // stores the coupling between input fields and their output name
        [Serializable]
        public class Gate
        {
            public string outputFieldName;

            [SerializeReference]
            public BaseField field;

            public override string ToString()
            {
                return $"{outputFieldName} Gate <{(field != null ? field.ToString() : "none")}>";
            }
        }

        [SerializeField]
        protected List<Gate> gates;

        [Serializable]
        public class OutputOverride
        {
            public string outputFieldName;
            public int value;
        }

        [SerializeField]
        protected List<OutputOverride> overrides;

        protected void OnEnable()
        {
            foreach (var gate in gates.ToArray())  // might manipulate gates within the loop
            {
                if (string.IsNullOrEmpty(gate.outputFieldName))
                {
                    Debug.LogWarning($"Removing empty gate {gate}", this);
                    RemoveGate(gate);
                    continue;
                }
                // initialize 
                InitializeFields(gate, new BaseField[] { gate.field });
            }

            CacheOverrides();
        }
        protected void OnDisable()
        {
            foreach (var gate in gates.ToArray())
            {
                FinalizeFields(gate, new BaseField[] { gate.field });
            }
        }

        void InitializeFields(Gate gate, IEnumerable<BaseField> fields)
        {
            // make sure output field for gate is initialized
            GetOutputField(gate.outputFieldName);

            // initialize all fields
            fields.ToList().ForEach(f =>
            {
                if (f == null || f is OutputField)
                    return;

                Manager.Instance.RegisterField(f);
                try
                {
                    f.Initialize(this);
                    InitializeFields(gate, f.GetUpstreamFields());
                }
                catch (BaseField.FieldInitializationException)
                {
                    Debug.LogWarning($"caught FieldInitializationException, removing {gate}", this);
                    RemoveGate(gate);
                    return;
                }
            });
        }

        void FinalizeFields(Gate gate, IEnumerable<BaseField> fields)
        {
            // finalize all gate fields and output fields
            fields.Concat(outputFields.Values).ToList().ForEach(f =>
            {
                if (f == null || f is OutputField)
                    return;

                f.Finalize(this);
                Manager.Instance?.UnregisterField(f);
                FinalizeFields(gate, f.GetUpstreamFields());
            });
        }

        public List<Gate> Gates => gates;
        int gateIncrement;
        public void AddGate(Gate gate)
        {
            gateIncrement++;
            gates.Add(gate);

            InitializeFields(gate, new[] { gate.field });
        }
        public void RemoveGate(Gate gate)
        {
            gateIncrement++;
            gates.Remove(gate);
            FinalizeFields(gate, new[] { gate.field });
        }

        // output fields of this node
        protected Dictionary<string, OutputField> outputFields = new Dictionary<string, OutputField>();
        public Dictionary<string, OutputField> GetOutputFields() => outputFields;
        public OutputField GetOutputField(string name)
        {
            // lazy initialization
            OutputField output;
            if (!outputFields.TryGetValue(name, out output))
            {
                output = new OutputField(name);
                output.Initialize(this);
                outputFields[name] = output;
            }

            return output;
        }

        int overridesIncrement;
        Dictionary<string, OutputOverride> cachedOverrides = new Dictionary<string, OutputOverride>();
        public Dictionary<string, OutputOverride> GetOverrides() => cachedOverrides;
        public void SetOverride(string name, int value)
        {
            if (!cachedOverrides.ContainsKey(name))
            {
                overrides.Add(new OutputOverride { outputFieldName = name });
                CacheOverrides();
            }
            var overrideOutput = cachedOverrides[name];
            overrideOutput.value = value;
        }
        public void ClearOverride(string name)
        {
            if (cachedOverrides.ContainsKey(name))
            {
                overrides.Remove(cachedOverrides[name]);
                CacheOverrides();
            }
            else
            {
                Debug.LogWarning($"clearing undefined override {name}");
            }
        }

        void CacheOverrides()
        {
            cachedOverrides.Clear();
            foreach (var o in overrides)
            {
                cachedOverrides[o.outputFieldName] = o;
            }
            overridesIncrement++;
        }

#if UNITY_EDITOR
        // update overrides every frame to allow setting overrides from editor
        void LateUpdate()
        {
            CacheOverrides();
        }
#endif
    }
}
