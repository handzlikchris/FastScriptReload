using UnityEngine;

public class OtherSingletonTest : MonoBehaviour
{
    [SerializeField] public string _stringValue = "test 1 other singleton";

    private static OtherSingletonTest _instance;
    public static OtherSingletonTest Instance => _instance ?? (_instance = new OtherSingletonTest());
    [SerializeField] public int _val = 1;

    [ContextMenu(nameof(PrintExistingSingletonValue))]
    void PrintExistingSingletonValue()
    {
        Debug.Log($"PrintExistingSingletonValue-c: {ExistingSingletonTest.Instance._intValue}"); 
    }

    private void Update()
    {
        Debug.Log($"TEst dyn: {_val}");
    }
}
