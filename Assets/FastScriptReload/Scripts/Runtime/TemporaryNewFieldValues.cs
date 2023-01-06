using System;
using System.Collections.Generic;
using System.Dynamic;

namespace FastScriptReload.Scripts.Runtime
{
    public static class TemporaryNewFieldValues
    {
        private static readonly Dictionary<object, ExpandoForType> _existingObjectToFiledNameValueMap = new Dictionary<object, ExpandoForType>();
        private static readonly Dictionary<Type, Dictionary<string, Func<object>>> _existingObjectTypeToFieldNameToCreateDetaultValueFn = new Dictionary<Type, Dictionary<string, Func<object>>>();

        public static void RegisterNewFields(Type existingType, Dictionary<string, Func<object>> fieldNameToGenerateDefaultValueFn)
        {
            _existingObjectTypeToFieldNameToCreateDetaultValueFn[existingType] = fieldNameToGenerateDefaultValueFn;
        }
            
        public static dynamic ResolvePatchedObject<T>(object original)
        {
            if (!_existingObjectToFiledNameValueMap.TryGetValue(original, out var existingExpandoToObjectTypePair))
            {
                var patchedObject = new ExpandoObject();
                var expandoForType = new ExpandoForType { ForType = typeof(T), Object = patchedObject };
                
                InitializeAdditionalFieldValues<T>(original, patchedObject);
                _existingObjectToFiledNameValueMap[original] = expandoForType;

                return patchedObject;
            }
            else
            {
                if (existingExpandoToObjectTypePair.ForType != typeof(T))
                {
                    InitializeAdditionalFieldValues<T>(original, existingExpandoToObjectTypePair.Object);
                    existingExpandoToObjectTypePair.ForType = typeof(T);
                }

                return existingExpandoToObjectTypePair.Object;
            }
        }
        
        public static bool TryGetDynamicallyAddedFieldValues(object forObject, out IDictionary<string, object> addedFieldValues)
        {
            if (_existingObjectToFiledNameValueMap.TryGetValue(forObject, out var expandoForType))
            {
                addedFieldValues = expandoForType.Object;
                return true;
            }

            addedFieldValues = null;
            return false;
        }

        private static void InitializeAdditionalFieldValues<T>(object original, ExpandoObject patchedObject)
        {
            var originalType = original.GetType(); //TODO: PERF: resolve via TOriginal, not getType
            var patchedObjectAsDict = patchedObject as IDictionary<string, Object>;
            foreach (var fieldNameToGenerateDefaultValueFn in _existingObjectTypeToFieldNameToCreateDetaultValueFn[originalType])
            {
                if (!patchedObjectAsDict.ContainsKey(fieldNameToGenerateDefaultValueFn.Key))
                {
                    patchedObjectAsDict[fieldNameToGenerateDefaultValueFn.Key] = fieldNameToGenerateDefaultValueFn.Value();
                }
            }
        }
    }

    public class ExpandoForType {
        public Type ForType;
        public ExpandoObject Object;
    }
}