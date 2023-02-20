using UnityEngine;
using UnityEditor;

// namespace TestNamespace //readd for testing, doesn't work well with linqpad
// {
    public class AddNewFieldsTest : MonoBehaviour
    {
        private const int TestConst = 5;
        private static int TestStatic = 5;

        [SerializeField] private int testVal = 10;
        [SerializeField] private int testVal2 = 20;
        
        // [SerializeField] private string newString = "test new string ";
        
        // [SerializeField] private int testInt = 20;
        // [SerializeField] private bool testBool = true; 
        // [SerializeField] private float testFloat = 1.5f; 
        // [SerializeField] private Color testColor;
        // [SerializeField] private TestEnum testEnum;
        // [SerializeField] private Vector2 testVector2;
        // [SerializeField] private Vector3 testVector3 = new Vector3(0, 5 , 1);
        // [SerializeField] private Vector4 testVector4;
        // [SerializeField] private Rect testRect;
        // [SerializeField] private AnimationCurve testAnimationCurve;
        // [SerializeField] private Bounds testBounds;
        // [SerializeField] private Gradient testGradient;
        // [SerializeField] private Vector2Int testVector2Int;
        // [SerializeField] private Vector3Int testVector3Int = new Vector3Int(0, 0, 0);
        // [SerializeField] private RectInt testRectInt;
        // [SerializeField] private BoundsInt testBoundsInt;
        // [SerializeField] private Quaternion testQuaternion;
        //
        // [SerializeField] private AddNewFieldReference newObject;
        //
        // [SerializeField] private Quaternion angle;
        // public Vector3 Angle => angle.eulerAngles; 

        void Update()
        {
            // Debug.Log("test");
            
            // Debug.Log($"{newString}test{tesInt}bool: {testBool}, float: {testFloat}");
            
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

        [ContextMenu(nameof(DebugDynamicValues))]
        public void DebugDynamicValues()
        {
            // Debug.Log(newObject.TestString);
            // newObject.Test();
            
            // Debug.Log($"testInt: {testInt}");
            // Debug.Log($"testBool: {testBool}");
            // Debug.Log($"testFloat: {testFloat}"); 
            // Debug.Log($"testColor: {testColor}");
            // Debug.Log($"testEnum: {testEnum}");
            // Debug.Log($"testVector2: {testVector2}");
            // Debug.Log($"testVector3: {testVector3}");
            // Debug.Log($"testVector4: {testVector4}");
            // Debug.Log($"testRect: {testRect}");
            // Debug.Log($"testAnimationCurve: {testAnimationCurve}");
            // Debug.Log($"testBounds: {testBounds}");
            // Debug.Log($"testGradient: {testGradient}");
            // Debug.Log($"testVector2Int: {testVector2Int}");
            // Debug.Log($"testVector3Int: {testVector3Int}");
            // Debug.Log($"testRectInt: {testRectInt}");
            // Debug.Log($"testBoundsInt: {testBoundsInt}");
            // Debug.Log($"testQuaternion: {testQuaternion}");
            
            // Debug.Log(nameof(testInt));
            // Debug.Log(nameof(TestStatic));
            // Debug.Log(nameof(newObject));

            
            // Debug.Log(TestConst + TestStatic + 1);
            // Debug.Log(Angle);
        }

        void OnScriptHotReload()
        {
            // Debug.Log(newString + "test" + testVal3);
        }
    }


    //TODO: custom editors dont work
    [CustomEditor(typeof(AddNewFieldsTest))]
    public class AddNewFieldsTestEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI(); 
    
            if (GUILayout.Button("Debug Values"))
            {
                ((AddNewFieldsTest)target).DebugDynamicValues();
            }
        }
    }


    public enum TestEnum
    {
        Value0,
        Value1,
        Value2
    } 
// }