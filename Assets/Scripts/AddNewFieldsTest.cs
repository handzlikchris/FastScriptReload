
using UnityEngine;

// namespace TestNamespace //readd for testing, doesn't work well with linqpad
// {
    public class AddNewFieldsTest : MonoBehaviour
    {
        [SerializeField] private int testVal = 10;
        [SerializeField] private int testVal2 = 20;
        
        [SerializeField] private string newString = "test new string ";
        [SerializeField] private int testVal3 = 20; 
        //
        [SerializeField] private bool testBool = true; 
        [SerializeField] private float testFloat = 1.5f; 
        [SerializeField] private Color testColor;

        
        void Update()
        {
            Debug.Log($"{newString}test{testVal3}bool: {testBool}, float: {testFloat}");
            Debug.Log($"color: {testColor}"); //Causes some issues

            // Debug.Log(newString + "test" + testVal3);
            
            // dynamic expando = new ExpandoObject();
            // expando.test = "123";
            
            // Debug.Log($"AddNewFields: {testVal} + {testVal2} + str + {testVal3}");  
            // Debug.Log($"Test: {str}");
            // Debug.Log("Expando test: {expando.test}");
            
            // Debug.Log($"Test: {newString} 123");
            //
            // newString = "str changed!";
        }

        void OnScriptHotReload()
        {
            // Debug.Log(newString + "test" + testVal3);
        }
    }
// }