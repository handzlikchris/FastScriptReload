using Extensions;
using UnityEngine;

namespace DefaultNamespace
{
    public class ExtensionTest: MonoBehaviour
    {
        [ContextMenu("Test")]
        public void TestCall()
        {
            Debug.Log("TestCall 3 ");
            var dir = CardinalDirection.East;
            var other = dir.Flip();
        
            Debug.Log("other" + other);
        }    
    }
}