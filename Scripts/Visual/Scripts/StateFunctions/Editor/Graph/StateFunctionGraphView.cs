using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using GraphProcessor;
using System;
using UnityEditor;
using System.Collections.Generic;

namespace OneHamsa.Dexterity.Visual
{
    public class StateFunctionGraphView : BaseGraphView
    {
        readonly string graphStyle = "StateFunctionGraph";

        public StateFunctionGraphView(EditorWindow window) : base(window) { }

        protected override void InitializeView()
        {
            styleSheets.Add(Resources.Load<StyleSheet>(graphStyle));
        }
    }
}