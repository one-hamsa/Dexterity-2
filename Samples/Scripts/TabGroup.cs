using OneHamsa.Dexterity;
using OneHamsa.Dexterity.Builtins;
using UnityEngine;

public class TabGroup : MonoBehaviour
{
    public int selectedTabIndex;
    [Field] public string focusFieldName;

    private void OnEnable()
    {
        SelectTab(selectedTabIndex);
        
        for (var i = 0; i < transform.childCount; i++)
        {
            var current = i;
            transform.GetChild(i).transform.GetComponent<ClickListener>()
                .onClick.AddListener(() => SelectTab(current));
        }
    }

    public void SelectTab(int index)
    {
        selectedTabIndex = index;
        for (var i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).GetComponent<FieldNode>()
                .GetOutputField(focusFieldName)
                .SetOverride(i == index);
        }
    }
}
