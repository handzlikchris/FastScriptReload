using System;
using System.Collections.Generic;
using ImmersiveVrToolsCommon.Runtime.Logging;
using MonoMod.Utils;
using UnityEngine;

namespace FastScriptReload.Editor.Compilation.CodeRewriting
{
    public class TypeNameToCreateValueFromRawCodeResolver
    {
        private static readonly Dictionary<string, Type> CoreTypeAliasToFullTypeName = new Dictionary<string, Type>()
        {
            ["object"] = typeof(object),
            ["string"] = typeof(string),
            ["bool"] = typeof(bool),
            ["byte"] = typeof(byte),
            ["char"] = typeof(char),
            ["decimal"] = typeof(decimal),
            ["double"] = typeof(double),
            ["short"] = typeof(short),
            ["int"] = typeof(int),
            ["long"] = typeof(long),
            ["sbyte"] = typeof(sbyte),
            ["float"] = typeof(float),
            ["ushort"] = typeof(ushort),
            ["uint"] = typeof(uint),
            ["ulong"] = typeof(ulong),
        };

        //TODO: PERF: this is used every time new value needs to be populated for new object (potentially many, ideally do some caching)
        public static object CreateValue(NewFieldDeclaration newFieldDeclaration)
        {
            //TODO: think about adding type creation to compiled class, this way could use reflection to get created fields and default values of correct type
            if (CoreTypeAliasToFullTypeName.TryGetValue(newFieldDeclaration.TypeName, out var t))
            {
                return GetDefaultValue(t);
            }
            
            var type = ReflectionHelper.GetType(newFieldDeclaration.TypeName);
            if (type == null)
            {
                LoggerScoped.LogWarning($"Unable to resolve type for '{newFieldDeclaration.TypeName}', returning null as default vaule");
                return null;
            }
            return GetDefaultValue(type);
        }
        
        private static object GetDefaultValue(Type t)
        {
            if (t == typeof(string))
            {
                return string.Empty;
            }
            
            if (t.IsValueType)
                return Activator.CreateInstance(t);

            return null;
        }
    }
}