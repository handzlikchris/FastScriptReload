//Example comes from https://bitbucket.org/catlikecodingunitytutorials/basics-02-building-a-graph/src/master/

using System.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FastScriptReload.Examples
{
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

		[ContextMenu(nameof(ResetIterationCounter))]
		void ResetIterationCounter()
		{
			_testIterationCounter = 0; 
		}

		void Update() 
		{
			var f = FunctionLibrary.GetFunction(function);
			var time = Time.time;
			var step = 2f / resolution;
			var v = 0.5f * step - 1f;
			for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++)
			{
				if (x == resolution)
				{
					x = 0;
					z += 1;
					v = (z + 0.5f) * step - 1f;
				}

				var u = (x + 0.5f) * step - 1f;
				points[i].localPosition = f(u + _testUMove, v, time);
			}

			_testIterationCounter++;
		}

		void OnScriptHotReload()
		{
			Debug.Log($"Script 'Graph.cs' was changed and hot reloaded, you have access to instance via 'this', eg: {nameof(_testIterationCounter)} value is: {_testIterationCounter}"); 
		}
		
		static void OnScriptHotReloadNoInstance()
		{
			Debug.Log("Script 'Graph.cs' was changed - this method is executed even without any instance in the scene. There's no access to 'this'. " +
			          "Useful if you just added a type / need to perform some one-off init"); 
		}
	}
}

// Dynamically adding new types, OnScriptHotReloadNewTypeAdded will trigger and allow setup, re-add for test
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
 // 		Debug.Log("test");
 // 	}
 //
 // 	void OnScriptHotReload()
 // 	{
 // 		Debug.Log("Script hot reloaded -NewMonoBehaviourTest"); 
 // 	}
 // }