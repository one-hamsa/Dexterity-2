using System;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

public class HeatMeter : MonoBehaviour
{
    public Slider slider;
    public TextMeshProUGUI text;
    
    public enum HeatLevel
    {
        Low,
        Medium,
        High,
        Critical
    }
    
    public float minForMedium = 0.3f;
    public float minForHigh = 0.6f;
    public float minForCritical = 0.9f;
    
    [Preserve]
    public HeatLevel GetHeatLevel() => slider.value switch 
    {
        var v when v < minForMedium => HeatLevel.Low,
        var v when v < minForHigh => HeatLevel.Medium,
        var v when v < minForCritical => HeatLevel.High,
        _ => HeatLevel.Critical
    };

    private void Update()
    {
        text.text = GetHeatLevel().ToString();
    }
}
