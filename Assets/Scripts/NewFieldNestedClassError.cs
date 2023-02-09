using System;
using UnityEngine;

public class NewFieldNestedClassError : MonoBehaviour
{
    public int test1 = 5;
    
    void Update()
    {

    }

    [ContextMenu("Test")]
    void Test()
    {
        Debug.Log($"Test2 + {test1}");
    }
    
    [Serializable]
    public class LevelExitData
    {
        public LevelExit LevelExit => _levelExit;
        public int ChannelID => _channelID;

        // Set by editor script
        [Header("Exit")]
        // [MMReadOnly]
        [SerializeField] public string ToLevel;

        // set with the inspector
        [Header("Data")]
        [SerializeField] private LevelExit _levelExit;
        // [MMReadOnly]
        [SerializeField] public string FromLevel;
        [SerializeField] private int _channelID = 0;

        public LevelExitData(string fromLevel, string toLevel)
        {
            FromLevel = fromLevel;
            ToLevel = toLevel;
        }
    }

    public class LevelExit
    {
        
    }
}
