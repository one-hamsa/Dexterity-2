using System;
using OneHamsa.Dexterity;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

public class HeatMeterOverrideSetter : MonoBehaviour
{
    public BaseStateNode node;
    [State(objectFieldName: nameof(node))] 
    public string lowHeatState;

    private int lowHeatStateId = -1;
    
    private void Awake()
    {
        lowHeatStateId = Database.instance.GetStateID(lowHeatState);
    }

    [Preserve]
    public void SetOverrideHeatLevelLow() => node.SetStateOverride(lowHeatStateId);

    [Preserve]
    public void ClearOverrideHeatLevel() => node.ClearStateOverride();
}
