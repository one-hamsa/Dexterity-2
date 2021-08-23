using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [CreateAssetMenu(menuName = "Dexterity/Settings", fileName = "Dexterity Settings")]
    public class DexteritySettings : ScriptableObject
    {
        public FieldDefinition[] fieldDefinitions;
        public List<StateFunctionGraph> stateFunctions;
    }
}