using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImmersiveVRTools.Runtime.Common.Utilities;
using UnityEditor;
using UnityEngine;

public class ChangeFileTester : MonoBehaviour
{
    [SerializeField] private float _delayBeforeChange;
    
    [SerializeField] private List<string> _doNotIncludeIfTypeNameStartsWith;
    [SerializeField] private MonoScript _startFromScript;
    [SerializeField] private List<MonoScript> _scriptsToTest;
    [SerializeField] private List<MonoScript> _forceExcludeScriptFromTest;


    [ContextMenu(nameof(StartTesting))]
    void StartTesting()
    {
        StartCoroutine(AddEmptyLineToEachFile());
    }

    IEnumerator AddEmptyLineToEachFile()
    {
        var initIndex = 0;
        if (_startFromScript != null)
        {
            initIndex = _scriptsToTest.IndexOf(_startFromScript);
        }
        
        for (var i = initIndex; i < _scriptsToTest.Count; i++)
        {
            var script = _scriptsToTest[i];
            if (_forceExcludeScriptFromTest.Contains(script))
            {
                continue;
            }
            
            var filePath = AssetDatabase.GetAssetPath(script);
            File.AppendAllText(filePath, Environment.NewLine);
            Debug.Log($"Changed: {filePath} ({i} out of {_scriptsToTest.Count})");
            yield return new WaitForSeconds(_delayBeforeChange);
        }
    }
    
    [ContextMenu(nameof(LoadAllScripts))]
    void LoadAllScripts()
    {
        _scriptsToTest = ReflectionHelper.GetAllTypes().Where(t => typeof(MonoBehaviour).IsAssignableFrom(t)
            && _doNotIncludeIfTypeNameStartsWith.All(sw => !t.FullName.StartsWith(sw)))
            .Select(m =>
            {
                try
                {
                    var c = gameObject.AddComponent(m);
                    var script = MonoScript.FromMonoBehaviour((MonoBehaviour)c);
                    DestroyImmediate(c);
                    return script;
                }
                catch (Exception)
                {
                    return null;
                }

            })
            .Where(s => s != null)
            .ToList();
    }
}
