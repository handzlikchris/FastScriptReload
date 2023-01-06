using UnityEngine;

// namespace TestNamespace //readd for testing, doesn't work well with linqpad
// {
    public class AddNewFieldsTest : MonoBehaviour
    {
        [SerializeField] private int testVal = 10;
        [SerializeField] private int testVal2 = 20;
        [SerializeField] private string newString = "test new string ";
        
        void Update()
        {
            // dynamic expando = new ExpandoObject();
            // expando.test = "123";
            
            // Debug.Log($"AddNewFields: {testVal} + {testVal2} + str + {testVal3}");  
            // Debug.Log($"Test: {str}");
            // Debug.Log("Expando test: {expando.test}");
            
            // Debug.Log($"Test: {newString} 123");
            //
            // newString = "str changed!";
            Debug.Log(newString);
        }

        void OnScriptHotReload()
        {
            // testVal3 = 20; 
        }
    }
// }