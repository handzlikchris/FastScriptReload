using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using UnityEditor;
using UnityEngine;

public class ProgresBarTest : MonoBehaviour
{
    public float _startSecond;
    public float _runForSeconds;
    public float _addProgressEverySecond;


    [ContextMenu("Progress Bar Test")]
    void ShowProgressBar()
    {
        EditorUtility.ClearProgressBar();
        StartCoroutine(DisplayProgressBar(_startSecond, _runForSeconds, _addProgressEverySecond));
    }
    

    IEnumerator DisplayProgressBar(float startSecond, float runForSeconds, float addProgressEverySecond)
    {
        var runningFor = 0f;

        do
        {
            EditorUtility.DisplayProgressBar($"Reload Script Assemblies (busy for {(int)Math.Floor(startSecond + runningFor)}s)...", "Reload Script Assemblies", 0.3f + (addProgressEverySecond * runningFor));
            runningFor += Time.deltaTime;
            yield return null;
        } while (runningFor < runForSeconds);
        

        EditorUtility.ClearProgressBar();
    }
}
