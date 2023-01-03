using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;

namespace FastScriptReload.Editor.NewFields
{
    public static class TemporaryNewFieldValues
    {
        //TODO: how to refresh on additional patch, would need to retain previous values...?

        public static Dictionary<object, ExpandoForType> _existingObjectToFiledNameValueMap = new Dictionary<object, ExpandoForType>();

        //TODO: detect re-patch
        public static dynamic ResolvePatchedObject<T>(object original)
            where T: new() //TODO: try to get requirement removed - technically can use roslyn to get default values from file and init ini this manner
        {
            if (!_existingObjectToFiledNameValueMap.TryGetValue(original, out var val))
            {
                var patchedObject = new ExpandoObject();
                var expandoForType = new ExpandoForType { ForType = typeof(T), Object = patchedObject };
			
                var instanceOfT = new T();
                var patchedObjectAsDict = patchedObject as IDictionary<string, Object>;
                foreach(var fieldInfo in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) //TODO: get other members as well
                {//TODO: add only new
                    patchedObjectAsDict[fieldInfo.Name] = fieldInfo.GetValue(instanceOfT);
                }
			
                _existingObjectToFiledNameValueMap[original] = expandoForType;

                return patchedObject;
            }
            else
            {
                if (val.ForType != typeof(T))
                {
                    var instanceOfT = new T();
                    var patchedObjectAsDict = val.Object as IDictionary<string, Object>;
                    foreach (var fieldInfo in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) //TODO: get other members as well
                    {
                        if (!patchedObjectAsDict.ContainsKey(fieldInfo.Name)) { //only init if not yet there
                            patchedObjectAsDict[fieldInfo.Name] = fieldInfo.GetValue(instanceOfT);
                        }
                    }

                    val.ForType = typeof(T);
                }

                return val.Object;
            }
        }
    }

    public class ExpandoForType {
        public Type ForType;
        public ExpandoObject Object;
    }
}