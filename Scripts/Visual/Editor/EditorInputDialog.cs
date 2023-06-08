/// from https://forum.unity.com/threads/is-there-a-way-to-input-text-using-a-unity-editor-utility.473743/#post-7229248
using System;
using UnityEditor;
using UnityEngine;
 
namespace OneHamsa.Dexterity.Visual
{
    public abstract class EditorInputDialog<T> : EditorWindow
    {
        private T inputValue;
        string description;
        string  okButton, cancelButton;
        bool    initializedPosition = false;
        Action  onOKButton;
    
        bool    shouldClose = false;
        Vector2 maxScreenPos;

        protected abstract T ShowInput(T value);
    
        #region OnGUI()
        void OnGUI()
        {
            // Check if Esc/Return have been pressed
            var e = Event.current;
            if( e.type == EventType.KeyDown )
            {
                switch( e.keyCode )
                {
                    // Escape pressed
                    case KeyCode.Escape:
                        shouldClose = true;
                        e.Use();
                        break;
    
                    // Enter pressed
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        onOKButton?.Invoke();
                        shouldClose = true;
                        e.Use();
                        break;
                }
            }
    
            if( shouldClose ) {  // Close this dialog
                Close();
                //return;
            }
    
            // Draw our control
            var rect = EditorGUILayout.BeginVertical();
    
            EditorGUILayout.Space( 12 );
            EditorGUILayout.LabelField( description );
    
            EditorGUILayout.Space( 8 );
            GUI.SetNextControlName( "inText" );
            inputValue = ShowInput(inputValue);
            GUI.FocusControl( "inText" );   // Focus text field
            EditorGUILayout.Space( 12 );
    
            // Draw OK / Cancel buttons
            var r = EditorGUILayout.GetControlRect();
            r.width /= 2;
            if( GUI.Button( r, okButton ) ) {
                onOKButton?.Invoke();
                shouldClose = true;
            }
    
            r.x += r.width;
            if( GUI.Button( r, cancelButton ) ) {
                inputValue = default;   // Cancel - delete inputText
                shouldClose = true;
            }
    
            EditorGUILayout.Space( 8 );
            EditorGUILayout.EndVertical();
    
            // Force change size of the window
            if( rect.width != 0 && minSize != rect.size ) {
                minSize = maxSize = rect.size;
            }
    
            // Set dialog position next to mouse position
            if( !initializedPosition && e.type == EventType.Layout )
            {
                initializedPosition = true;
    
                // Move window to a new position. Make sure we're inside visible window
                var mousePos = GUIUtility.GUIToScreenPoint( Event.current.mousePosition );
                mousePos.x += 32;
                if( mousePos.x + position.width > maxScreenPos.x ) mousePos.x -= position.width + 64; // Display on left side of mouse
                if( mousePos.y + position.height > maxScreenPos.y ) mousePos.y = maxScreenPos.y - position.height;
    
                position = new Rect( mousePos.x, mousePos.y, position.width, position.height );
    
                // Focus current window
                Focus();
            }
        }
        #endregion OnGUI()
    
        #region Show()

        /// <summary>
        /// Returns text player entered, or null if player cancelled the dialog.
        /// </summary>
        /// <param name="window"></param>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <param name="inputValue"></param>
        /// <param name="okButton"></param>
        /// <param name="cancelButton"></param>
        /// <returns></returns>
        protected static T Show(EditorInputDialog<T> window, string title, string description, T inputValue, string okButton = "OK", string cancelButton = "Cancel" )
        {
            // Make sure our popup is always inside parent window, and never offscreen
            // So get caller's window size
            var maxPos = GUIUtility.GUIToScreenPoint( new Vector2( Screen.width, Screen.height ) );
    
            T ret = default;
            window.maxScreenPos = maxPos;
            window.titleContent = new GUIContent( title );
            window.description = description;
            window.inputValue = inputValue;
            window.okButton = okButton;
            window.cancelButton = cancelButton;
            window.onOKButton += () => ret = window.inputValue;
            //window.ShowPopup();
            window.ShowModal();
    
            return ret;
        }
        #endregion Show()
    }
    
    public class EditorInputStringDialog : EditorInputDialog<string>
    {
        protected override string ShowInput(string value) => EditorGUILayout.TextField("", value);
        
        public static string Show(string title, string description, string inputValue, 
            string okButton = "OK", string cancelButton = "Cancel" )
        {
            var win = CreateInstance<EditorInputStringDialog>();
            return Show(win, title, description, inputValue, okButton, cancelButton);
        }
    }
    
    public class EditorInputIntDialog : EditorInputDialog<int>
    {
        protected override int ShowInput(int value) => EditorGUILayout.IntField("", value);
        
        public static int Show(string title, string description, int inputValue, 
            string okButton = "OK", string cancelButton = "Cancel" )
        {
            var win = CreateInstance<EditorInputIntDialog>();
            return Show(win, title, description, inputValue, okButton, cancelButton);
        }
    }
    
    public class EditorInputFloatDialog : EditorInputDialog<float>
    {
        protected override float ShowInput(float value) => EditorGUILayout.FloatField("", value);
        
        public static float Show(string title, string description, float inputValue, 
            string okButton = "OK", string cancelButton = "Cancel" )
        {
            var win = CreateInstance<EditorInputFloatDialog>();
            return Show(win, title, description, inputValue, okButton, cancelButton);
        }
    }
    
    public class EditorInputColorDialog : EditorInputDialog<Color>
    {
        protected override Color ShowInput(Color value) => EditorGUILayout.ColorField("", value);
        
        public static Color Show(string title, string description, Color inputValue, 
            string okButton = "OK", string cancelButton = "Cancel" )
        {
            var win = CreateInstance<EditorInputColorDialog>();
            return Show(win, title, description, inputValue, okButton, cancelButton);
        }
    }
    
    public class EditorInputBoundsIntDialog : EditorInputDialog<BoundsInt>
    {
        protected override BoundsInt ShowInput(BoundsInt value) => EditorGUILayout.BoundsIntField("", value);
        
        public static BoundsInt Show(string title, string description, BoundsInt inputValue, 
            string okButton = "OK", string cancelButton = "Cancel" )
        {
            var win = CreateInstance<EditorInputBoundsIntDialog>();
            return Show(win, title, description, inputValue, okButton, cancelButton);
        }
    }
}