using UnityEngine;

namespace Test.TestingNested
{
    public class HangTest: MonoBehaviour { 
        [ContextMenu(nameof(Call))]
        public void Call() {
            var impl = new BaseClassImpl();
            impl.TestCall(); 
            //1. execute to check it works
            //2. make change to BaseClassImpl and let it hot-reload
            //3. call again, should freeze editor
        }
    }
}
