using System;
using System.Collections.Generic;
using System.Dynamic;
using UnityEngine;
using Object = System.Object;

namespace FastScriptReload.Scripts.Runtime
{
    public static class TemporaryNewFieldValues
    {
        public delegate object GetNewFieldInitialValue(Type forNewlyGeneratedType);
        public delegate Type GetNewFieldType(Type forNewlyGeneratedType);

        private static readonly Dictionary<object, ExpandoForType> _existingObjectToFiledNameValueMap = new Dictionary<object, ExpandoForType>();
        private static readonly Dictionary<Type, Dictionary<string, GetNewFieldInitialValue>> _existingObjectTypeToFieldNameToCreateDetaultValueFn = new Dictionary<Type, Dictionary<string, GetNewFieldInitialValue>>();
        private static readonly Dictionary<Type, Dictionary<string, GetNewFieldType>> _existingObjectTypeToFieldNameToType = new Dictionary<Type, Dictionary<string, GetNewFieldType>>();
        
        //Unity by default will auto init some classes, like gradient, but those are not value types so need to be initialized manually
        private static Dictionary<Type, Func<object>> ReferenceTypeToCreateDefaultValueFn = new Dictionary<Type, Func<object>>()
        {
            [typeof(Gradient)] = () => new Gradient(),
            [typeof(AnimationCurve)] = () => new AnimationCurve(),
        };
        
        public static void RegisterNewFields(Type existingType, Dictionary<string, GetNewFieldInitialValue> fieldNameToGenerateDefaultValueFn, Dictionary<string, GetNewFieldType> fieldNameToGetTypeFn)
        {
            _existingObjectTypeToFieldNameToCreateDetaultValueFn[existingType] = fieldNameToGenerateDefaultValueFn;
            _existingObjectTypeToFieldNameToType[existingType] = fieldNameToGetTypeFn;
        }
        
        public static dynamic ResolvePatchedObject<TCreatedType>(object original)
        {
            if (!_existingObjectToFiledNameValueMap.TryGetValue(original, out var existingExpandoToObjectTypePair))
            {
                var patchedObject = new ExpandoObject();
                var expandoForType = new ExpandoForType { ForType = typeof(TCreatedType), Object = patchedObject };
                
                InitializeAdditionalFieldValues<TCreatedType>(original, patchedObject);
                _existingObjectToFiledNameValueMap[original] = expandoForType;

                return patchedObject;
            }
            else
            {
                if (existingExpandoToObjectTypePair.ForType != typeof(TCreatedType))
                {
                    InitializeAdditionalFieldValues<TCreatedType>(original, existingExpandoToObjectTypePair.Object);
                    existingExpandoToObjectTypePair.ForType = typeof(TCreatedType);
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

        private static void InitializeAdditionalFieldValues<TCreatedType>(object original, ExpandoObject patchedObject)
        {
            var originalType = original.GetType(); //TODO: PERF: resolve via TOriginal, not getType
            var patchedObjectAsDict = patchedObject as IDictionary<string, Object>;
            foreach (var fieldNameToGenerateDefaultValueFn in _existingObjectTypeToFieldNameToCreateDetaultValueFn[originalType])
            {
                if (!patchedObjectAsDict.ContainsKey(fieldNameToGenerateDefaultValueFn.Key))
                {
                    patchedObjectAsDict[fieldNameToGenerateDefaultValueFn.Key] = fieldNameToGenerateDefaultValueFn.Value(typeof(TCreatedType));

                    if (patchedObjectAsDict[fieldNameToGenerateDefaultValueFn.Key] == null)
                    {
                       var fieldType = _existingObjectTypeToFieldNameToType[originalType][fieldNameToGenerateDefaultValueFn.Key](typeof(TCreatedType));
                       if (ReferenceTypeToCreateDefaultValueFn.TryGetValue(fieldType, out var createValueFn))
                       {
                           patchedObjectAsDict[fieldNameToGenerateDefaultValueFn.Key] = createValueFn();
                       }
                    }
                }
            }
        }
    }

    public class ExpandoForType {
        public Type ForType;
        public ExpandoObject Object;
    }
}