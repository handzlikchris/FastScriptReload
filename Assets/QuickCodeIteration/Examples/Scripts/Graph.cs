//Example comes from https://bitbucket.org/catlikecodingunitytutorials/basics-02-building-a-graph/src/master/

using System;
using UnityEngine;

public class Graph : MonoBehaviour {

	[SerializeField]
	Transform pointPrefab;

	[SerializeField, Range(10, 100)]
	int resolution = 10;

	[SerializeField]
	FunctionLibrary.FunctionName function;

	[SerializeField] private int _testIterationCounter = 1;
	[SerializeField] [Range(-3, 3)] private float _testUMove = 0f;

	Transform[] points;

	void Awake ()
	{
		var pointsHolderGo = new GameObject("PointsHolder");
		
		float step = 2f / resolution;
		var scale = Vector3.one * step;
		points = new Transform[resolution * resolution];
		for (int i = 0; i < points.Length; i++) {
			Transform point = points[i] = Instantiate(pointPrefab);
			point.localScale = scale;
			point.SetParent(pointsHolderGo.transform, false);
		}
	}

	void Update () {
		FunctionLibrary.Function f = FunctionLibrary.GetFunction(function);
		float time = Time.time;
		float step = 2f / resolution;
		float v = 0.5f * step - 1f;
		for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++) {
			if (x == resolution) {
				x = 0;
				z += 1;
				v = (z + 0.5f) * step - 1f;
			}
			float u = (x + 0.5f) * step - 1f;
			points[i].localPosition = f(u + _testUMove, v, time);
		}

		_testIterationCounter++;
	}

	void OnScriptHotReload()
	{
		Debug.Log("Script hot reloaded"); 
	}
}

//Dynamically adding new types, OnScriptHotReloadNewTypeAdded will trigger and allow setup, re-add for test
// public class NewMonoBehaviourTest : MonoBehaviour
// {
// 	static void OnScriptHotReloadNewTypeAdded()
// 	{
// 		var go = new GameObject("TestDynamic");
// 		go.AddComponent<NewMonoBehaviourTest>();
// 	}
//
// 	void Start()
// 	{
// 		Debug.Log("Start - NewMonoBehaviourTest");
// 		GameObject.CreatePrimitive(PrimitiveType.Cube);
// 	}
//
// 	private void Update()
// 	{
// 		Debug.Log("test 123");
// 	}
//
// 	void OnScriptHotReload()
// 	{
// 		Debug.Log("Script hot reloaded"); 
// 	}
// }