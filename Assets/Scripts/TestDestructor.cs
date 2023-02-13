using UnityEngine;

public class TestDestructor : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public TestDestructor()
    {
        Debug.Log("ctor ");
    }

    ~TestDestructor()
    {
        Debug.Log("Destroyed"); 
    }
}
