using OneHamsa.Dexterity;
using OneHamsa.Dexterity.Builtins;
using UnityEngine;
using UnityEngine.Events;

// very similar to DragOrClickListener but decoupled from MechUI
public class DragAndDropListener : MonoBehaviour
{
	public RaycastListener raycastListener;
	public float maxClickDuration = 0;

	private IRaycastController _controller;
	private bool _pressed;
	private float _pressDuration;
	
	public UnityEvent OnDragStart;
	public UnityEvent<Ray> OnDrag;
	public UnityEvent OnDragEnd;
	public UnityEvent OnDragCancel;
	public UnityEvent OnClick;

	private void FindRaycastListener() {
		if (raycastListener != null) return;
		
		raycastListener = GetComponentInChildren<RaycastListener>();
		if (raycastListener != null) return;
		
		raycastListener = GetComponentInParent<RaycastListener>();
		if (raycastListener != null) return;
		
		Debug.LogError("Unable to find RaycastListener");
		enabled = false;
	}
	
	private void OnPress() {
		_pressed = true;
		_pressDuration = 0;
		_controller = raycastListener.pressingController;
		OnDragStart?.Invoke();
		InvokeOnDrag();
	}
	
	private void OnRelease() {
		if (_pressDuration < maxClickDuration) {
			OnDragCancel?.Invoke();
			OnClick?.Invoke();
		} else {
			OnDragEnd?.Invoke();
		}
		
		_pressed = false;
		_controller = null;
	}

	private void InvokeOnDrag() {
		Ray ray = new Ray(_controller.position, _controller.forward);
		OnDrag?.Invoke(ray);
	}
	
	private void Awake() {
		FindRaycastListener();
	}

	private void OnEnable() {
		if (raycastListener == null) return;
		raycastListener.onPress += OnPress;
		raycastListener.onRelease += OnRelease;
	}

	private void OnDisable() {
		if (raycastListener == null) return;
		raycastListener.onPress -= OnPress;
		raycastListener.onRelease -= OnRelease;
	}

	private void Update() {
		if (_pressed == false) return;

		_pressDuration += Time.unscaledDeltaTime;
		InvokeOnDrag();
	}
}
