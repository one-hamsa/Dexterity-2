﻿using UnityEngine;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Builtins
{
    [RequireComponent(typeof(TransitionsListener))]
    public class RebuildLayoutOnTransition : MonoBehaviour
    {
        [SerializeField] private RectTransform targetTransform;
        [SerializeField] private bool forceRebuild = false;
        
        private TransitionsListener transitionsListener;

        private bool transitioning;

        protected virtual void Awake()
        {
            transitionsListener = GetComponent<TransitionsListener>();
            if (targetTransform == null)
                targetTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            transitionsListener.onTransitionsStart += OnTransitionsStart;
            transitionsListener.onTransitionsEnd += OnTransitionsEnd;
            
            Rebuild();
        }

        private void OnTransitionsStart(int oldState, int newState)
        {
            transitioning = true;
            Rebuild();
        }

        private void OnTransitionsEnd(int state)
        {
            transitioning = false;
            Rebuild();
        }

        private void Update()
        {
            if (!transitioning)
                return;
            
            Rebuild();
        }

        protected virtual void Rebuild()
        {
            LayoutRebuilder.MarkLayoutForRebuild(targetTransform);
            if (forceRebuild)
                LayoutRebuilder.ForceRebuildLayoutImmediate(targetTransform);
        }
    }
}