using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TmpProIssueTest : MonoBehaviour
{
    // dropDown
    [SerializeField] private TMP_Dropdown sortDropDown;
    
    public void Init()
    {
        UnityEngine.Debug.Log("CHANGE HERE");
        
        sortDropDown.options.Clear();
        sortDropDown.options.AddRange(new List<TMP_Dropdown.OptionData>()
        {
            new TMP_Dropdown.OptionData("latest_order"),
            new TMP_Dropdown.OptionData("old_order"), 
            new TMP_Dropdown.OptionData("order_of_progress"),
            new TMP_Dropdown.OptionData("order_of_low_progress")
        });
    }
}