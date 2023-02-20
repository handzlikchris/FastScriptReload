using UnityEngine;
using UnityEngine.UIElements;

public class IntTryParseIssue : MonoBehaviour
{
    private Dropdown dropdown = new Dropdown() { selectedText = new DropdownInner()};
    
    [ContextMenu(nameof(Test))]
    void Test()
    {
        int minCk = 0, maxCk = 0;

        try
        {
            if (int.TryParse(dropdown.selectedText.text.Split('-')[0], out minCk) == false) 
            {
                minCk = 10;
            } 
            
            if (int.TryParse(dropdown.selectedText.text.Split('-')[1], out maxCk) == false) 
            {
                minCk = 10;
            }
        }
        catch
        {
            
        }
    }
}

public class Dropdown
{
    public DropdownInner selectedText;
}

public class DropdownInner
{
    public string text;
}