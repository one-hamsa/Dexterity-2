using System;
using OneHamsa.Dexterity;
using OneHamsa.Dexterity.Builtins;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;
using UnityEngine.UI;

public class LongPressListener : ClickListener {

	public float pressDuration = 1f;
	public float fillStartsAt = 0.05f;

	private bool pressed;
	private float currentPressDuration;

	[Preserve] public bool IsPressed() => pressed;
	[Preserve] public float timeRemaining => pressDuration - currentPressDuration;

	protected override void Awake() 
	{
		base.Awake();
		OnPressUp();
	}

	protected override void OnEnable() 
	{
		base.OnEnable();
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
		pressed = true;
		currentPressDuration = fillStartsAt * pressDuration;
	}

	protected virtual void OnPressUp() 
	{
		pressed = false;
		UpdateProgress(0);
	}


	void Update()
	{
		if (pressed)
		{
			currentPressDuration += Time.unscaledDeltaTime;
			if (currentPressDuration >= pressDuration)
			{
				pressed = false;
				OnWaitCompleted();
			}
			else
			{
				float t = currentPressDuration / pressDuration;
				UpdateProgress(t);
			}
		}
	}

	protected virtual void UpdateProgress(float progress) { }

	protected virtual void OnWaitCompleted()
	{
		TriggerClick();
	}

	protected override void OnPressComplete()
	{
		// do nothing now
	}
}