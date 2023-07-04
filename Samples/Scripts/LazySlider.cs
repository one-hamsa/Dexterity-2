using System;
using OneHamsa.Dexterity;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider), typeof(TransitionInterpolator))]
public class LazySlider : MonoBehaviour
{
    public Slider sourceSlider;
    
    private Slider slider;
    private TransitionInterpolator interpolator;
    
    private void Awake()
    {
        slider = GetComponent<Slider>();
        interpolator = GetComponent<TransitionInterpolator>();
    }

    private void Update()
    {
        interpolator.SetTarget(sourceSlider.value);
        
        slider.value = interpolator.value;
    }
}
