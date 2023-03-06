using UnityEngine;

namespace Root
{
    public class ManualCodeGenerationOverload : MonoBehaviour
    {
        private void Update()
        {
            // MethodToChange1();
        }

        private void MethodToLeaveUntouched()
        {
            Debug.Log("MethodToLeaveUntouched");    
        }

        private void MethodToChange1()
        {
            Debug.Log("MethodToChange1 - 1");     
        }

        private void MethodToChange1(string arg1)
        { 
            Debug.Log("MethodToChange1 with arg1");  
        }

        private void MethodToChange1<T>(string arg1, int sad) 
        {
            void InnerFunction()
            {
                Debug.Log("Inner");
            }

            Debug.Log("MethodToChange1 with arg1"); 
        }

        private void MethodToChange2(string arg1)
        {
            Debug.Log("MethodToChange2 with arg1");
        }

        private void OnScriptHotReload()
        {
            Debug.Log("Hot reloaded");
            MethodToChange1();
            MethodToChange1<int>("test", 2);
        }

        public class NestedClass 
        {
            
        }
        
        public interface INestedInterface
        {
            
        }
    }
}