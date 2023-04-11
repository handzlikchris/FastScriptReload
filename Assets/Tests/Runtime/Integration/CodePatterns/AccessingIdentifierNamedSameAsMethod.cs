using UnityEngine;

namespace FastScriptReload.Tests.Runtime.Integration.CodePatterns
{
    public class AccessingIdentifierNamedSameAsMethod : MonoBehaviour
    {
        private AccessingIdentifierNamedSameAsMethodTesting testing;
        private AccessingIdentifierNamedSameAsMethod instance;
        
        public void TestInstanceFieldTypeRewritten()
        {
            instance = new AccessingIdentifierNamedSameAsMethod();
            //<confirmation-code>// TestDetourConfirmation.Confirm(this.GetType(), nameof(TestInstanceFieldTypeRewritten), typeof(AccessingIdentifierNamedSameAsMethod__Patched_).Name);
        }
        
        public void TestAccessingSameNamedVariableNotChanged() {
            testing = new AccessingIdentifierNamedSameAsMethodTesting();
            //<confirmation-code>// TestDetourConfirmation.Confirm(this.GetType(), nameof(TestAccessingSameNamedVariableNotChanged), testing.AccessingIdentifierNamedSameAsMethod);
        }
    }
    
    public class AccessingIdentifierNamedSameAsMethodTesting
    {
        public bool AccessingIdentifierNamedSameAsMethod { get; set; } = true;
    }
}