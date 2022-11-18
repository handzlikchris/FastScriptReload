using UnityEngine;

public class OtherSingletonTest : MonoBehaviour
{
    [SerializeField] public string _stringValue = "test 1 other singleton";
    
    public static OtherSingletonTest Instance;

    private void Start()
    {
        Instance = this;
    }
    
    [ContextMenu(nameof(PrintExistingSingletonValue))]
    void PrintExistingSingletonValue()
    {
        Debug.Log($"PrintExistingSingletonValue-c: {ExistingSingletonTest.Instance._intValue}"); 
    }
}
