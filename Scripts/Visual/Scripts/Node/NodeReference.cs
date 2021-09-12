using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using OneHumus.Data;

namespace OneHamsa.Dexterity.Visual
{
    [CreateAssetMenu(fileName = "New Node Reference", menuName = "Dexterity/Node Reference", order = 100)]
    public class NodeReference : ScriptableObject
    {
        // stores the coupling between input fields and their output name
        [Serializable]
        public class Gate
        {
            [Field]
            public string outputFieldName;

            [SerializeReference]
            public BaseField field;

            public int outputFieldDefinitionId { get; private set; } = -1;

            public bool Initialize(int fieldId = -1)
            {
                if (fieldId != -1)
                {
                    outputFieldDefinitionId = fieldId;
                    return true;
                }
                if (string.IsNullOrEmpty(outputFieldName))
                    return false;

                return (outputFieldDefinitionId = Manager.instance.GetFieldID(outputFieldName)) != -1;
            }

            public override string ToString()
            {
                return $"{outputFieldName} Gate <{(field != null ? field.ToString() : "none")}>";
            }
        }

        [SerializeField]
        public StateFunctionGraph stateFunction;

        [SerializeField]
        public List<Gate> gates;
    }
}
