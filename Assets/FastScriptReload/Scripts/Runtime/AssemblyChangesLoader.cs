#if UNITY_EDITOR || LiveScriptReload_Enabled

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ImmersiveVRTools.Runtime.Common;
using ImmersiveVRTools.Runtime.Common.Extensions;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FastScriptReload.Runtime
{
    [PreventHotReload]
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    public class AssemblyChangesLoader: IAssemblyChangesLoader
    {
        const BindingFlags ALL_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.Static | BindingFlags.Instance |
                                               BindingFlags.FlattenHierarchy;
            
        const BindingFlags ALL_DECLARED_METHODS_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
                                                                BindingFlags.Static | BindingFlags.Instance |
                                                                BindingFlags.DeclaredOnly; //only declared methods can be redirected, otherwise it'll result in hang
        
        public const string ClassnamePatchedPostfix = "__Patched_";
        public const string ON_HOT_RELOAD_METHOD_NAME = "OnScriptHotReload";
        public const string ON_HOT_RELOAD_NO_INSTANCE_STATIC_METHOD_NAME = "OnScriptHotReloadNoInstance";

        private static readonly List<Type> ExcludeMethodsDefinedOnTypes = new List<Type>
        {
            typeof(MonoBehaviour),
            typeof(Behaviour),
            typeof(UnityEngine.Object),
            typeof(Component),
            typeof(System.Object)
        }; //TODO: move out and possibly define a way to exclude all non-client created code? as this will crash editor
        
        private static AssemblyChangesLoader _instance;
        public static AssemblyChangesLoader Instance => _instance ?? (_instance = new AssemblyChangesLoader());

        private Dictionary<Type, Type> _existingTypeToRedirectedType = new Dictionary<Type, Type>();

        public void DynamicallyUpdateMethodsForCreatedAssembly(Assembly dynamicallyLoadedAssemblyWithUpdates, AssemblyChangesLoaderEditorOptionsNeededInBuild editorOptions)
        {
            try
            {
                var sw = new Stopwatch();
                sw.Start();
                
                var allTypesInNonDynamicGeneratedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.GetCustomAttributes<DynamicallyCreatedAssemblyAttribute>().Any())
                    .SelectMany(a => a.GetTypes())
                    .ToList();

                foreach (var createdType in dynamicallyLoadedAssemblyWithUpdates.GetTypes()
                             .Where(t => t.IsClass
                                         && !typeof(Delegate).IsAssignableFrom(t) //don't redirect delegates
                             )
                        )
                {
                    if (createdType.GetCustomAttribute<PreventHotReload>() != null)
                    {
                        //TODO: ideally type would be excluded from compilation not just from detour
                        Debug.Log($"Type: {createdType.Name} marked as {nameof(PreventHotReload)} - ignoring change.");
                        continue;
                    }
                    
                    var createdTypeNameWithoutPatchedPostfix = RemoveClassPostfix(createdType.FullName);
                    var matchingTypeInExistingAssemblies = allTypesInNonDynamicGeneratedAssemblies.SingleOrDefault(t => t.FullName == createdTypeNameWithoutPatchedPostfix);
                    if (matchingTypeInExistingAssemblies != null)
                    {
                        _existingTypeToRedirectedType[matchingTypeInExistingAssemblies] = createdType;
                        
                        if (!editorOptions.IsDidFieldsOrPropertyCountChangedCheckDisabled 
                            && !editorOptions.EnableExperimentalAddedFieldsSupport
                            && DidFieldsOrPropertyCountChanged(createdType,  matchingTypeInExistingAssemblies))
                        {
                            continue;
                        }

                        var allDeclaredMethodsInExistingType = matchingTypeInExistingAssemblies.GetMethods(ALL_DECLARED_METHODS_BINDING_FLAGS)
                            .Where(m => !ExcludeMethodsDefinedOnTypes.Contains(m.DeclaringType))
                            .ToList();
                        foreach (var createdTypeMethodToUpdate in createdType.GetMethods(ALL_DECLARED_METHODS_BINDING_FLAGS)
                                     .Where(m => !ExcludeMethodsDefinedOnTypes.Contains(m.DeclaringType)))
                        {
                            var createdTypeMethodToUpdateFullDescriptionWithoutPatchedClassPostfix = RemoveClassPostfix(createdTypeMethodToUpdate.FullDescription());
                            var matchingMethodInExistingType = allDeclaredMethodsInExistingType.SingleOrDefault(m => m.FullDescription() == createdTypeMethodToUpdateFullDescriptionWithoutPatchedClassPostfix);
                            if (matchingMethodInExistingType != null)
                            {
                                if (matchingMethodInExistingType.IsGenericMethod)
                                {
                                    Debug.LogWarning($"Method: '{matchingMethodInExistingType.FullDescription()}' is generic. Hot-Reload for generic methods is not supported yet, you won't see changes for that method.");
                                    continue;
                                }

                                if (matchingMethodInExistingType.DeclaringType != null && matchingMethodInExistingType.DeclaringType.IsGenericType)
                                {
                                    Debug.LogWarning($"Type for method: '{matchingMethodInExistingType.FullDescription()}' is generic. Hot-Reload for generic types is not supported yet, you won't see changes for that type.");
                                    continue;
                                }
                                
#if ImmersiveVrTools_DebugEnabled
                                Debug.Log($"Trying to detour method, from: '{matchingMethodInExistingType.FullDescription()}' to: '{createdTypeMethodToUpdate.FullDescription()}'");
#endif
                                DetourCrashHandler.LogDetour(matchingMethodInExistingType.ResolveFullName());
                                Memory.DetourMethod(matchingMethodInExistingType, createdTypeMethodToUpdate);
                            }
                            else
                            {
                                Debug.LogWarning($"Method: {createdTypeMethodToUpdate.FullDescription()} does not exist in initially compiled type: {matchingTypeInExistingAssemblies.FullName}. " +
                                                 $"Adding new methods at runtime is not fully supported. \r\n" +
                                                 $"It'll only work new method is only used by declaring class (eg private method)\r\n" +
                                                 $"Make sure to add method before initial compilation.");
                            }
                        }
                        
                        FindAndExecuteStaticOnScriptHotReloadNoInstance(createdType);
                        FindAndExecuteOnScriptHotReload(matchingTypeInExistingAssemblies);
                    }
                    else
                    {
                        Debug.LogWarning($"Unable to find existing type for: '{createdType.FullName}', this is not an issue if you added new type");
                        FindAndExecuteStaticOnScriptHotReloadNoInstance(createdType);
                        FindAndExecuteOnScriptHotReload(createdType);
                    }
                }
                
                Debug.Log($"Hot-reload completed (took {sw.ElapsedMilliseconds}ms)");
            }
            finally
            {
                DetourCrashHandler.ClearDetourLog();
            }
        }
        
        public Type GetRedirectedType(Type forExistingType)
        {
            return _existingTypeToRedirectedType[forExistingType];
        }

        private static bool DidFieldsOrPropertyCountChanged(Type createdType, Type matchingTypeInExistingAssemblies)
        {
            var createdTypeFieldAndPropertiesCount = createdType.GetFields(ALL_BINDING_FLAGS).Length + createdType.GetProperties(ALL_BINDING_FLAGS).Length;
            var matchingTypeFieldAndPropertiesCount = matchingTypeInExistingAssemblies.GetFields(ALL_BINDING_FLAGS).Length + matchingTypeInExistingAssemblies.GetProperties(ALL_BINDING_FLAGS).Length;
            if (createdTypeFieldAndPropertiesCount != matchingTypeFieldAndPropertiesCount)
            {
                Debug.LogError($"It seems you've added/removed field to changed script. This is not supported and will result in undefined behaviour. Hot-reload will not be performed for type: {matchingTypeInExistingAssemblies.Name}" +
                               $"\r\n\r\nYou can skip the check and force reload anyway if needed, to do so go to: 'Window -> Fast Script Reload -> Start Screen -> Reload -> tick 'Disable added/removed fields check'");
                Debug.Log(
                    $"<color=orange>There's an experimental feature that allows to add new fields (which are adjustable in editor), to enable please:</color>" +
                    $"\r\n - Open Settings 'Window -> Fast Script Reload -> Start Screen -> New Fields -> tick 'Enable experimental added field support'");
                return true;
            }

            return false;
        }

        private static void FindAndExecuteStaticOnScriptHotReloadNoInstance(Type createdType)
        {
            var onScriptHotReloadStaticFnForType = createdType.GetMethod(ON_HOT_RELOAD_NO_INSTANCE_STATIC_METHOD_NAME,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (onScriptHotReloadStaticFnForType != null)
            {
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    onScriptHotReloadStaticFnForType.Invoke(null, null);
                });
            }
        }

        private static void FindAndExecuteOnScriptHotReload(Type type)
        {
            var onScriptHotReloadFnForType = type.GetMethod(ON_HOT_RELOAD_METHOD_NAME, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (onScriptHotReloadFnForType != null)
            {
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    foreach (var instanceOfType in GameObject.FindObjectsOfType(type)) //TODO: perf - could find them in different way?
                    {
                        onScriptHotReloadFnForType.Invoke(instanceOfType, null);
                    }
                });
            }
        }

        private static string RemoveClassPostfix(string fqdn)
        {
            return fqdn.Replace(ClassnamePatchedPostfix, string.Empty);
        }
    }
    
    
    [AttributeUsage(AttributeTargets.Assembly)]
    public class DynamicallyCreatedAssemblyAttribute : Attribute
    {
        public DynamicallyCreatedAssemblyAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class PreventHotReload : Attribute
    {
        
    }
    
    public interface IAssemblyChangesLoader
    {
        void DynamicallyUpdateMethodsForCreatedAssembly(Assembly dynamicallyLoadedAssemblyWithUpdates, AssemblyChangesLoaderEditorOptionsNeededInBuild editorOptions);
    }
    
    [Serializable]
    public class AssemblyChangesLoaderEditorOptionsNeededInBuild
    {
        public bool IsDidFieldsOrPropertyCountChangedCheckDisabled;
        public bool EnableExperimentalAddedFieldsSupport;

        public AssemblyChangesLoaderEditorOptionsNeededInBuild(bool isDidFieldsOrPropertyCountChangedCheckDisabled, bool enableExperimentalAddedFieldsSupport)
        {
            IsDidFieldsOrPropertyCountChangedCheckDisabled = isDidFieldsOrPropertyCountChangedCheckDisabled;
            EnableExperimentalAddedFieldsSupport = enableExperimentalAddedFieldsSupport;
        }

        [Obsolete("Needed for network serialization")]
        public AssemblyChangesLoaderEditorOptionsNeededInBuild()
        {
        }

        //WARN: make sure it has same params as ctor
        public void UpdateValues(bool isDidFieldsOrPropertyCountChangedCheckDisabled, bool enableExperimentalAddedFieldsSupport)
        {
            IsDidFieldsOrPropertyCountChangedCheckDisabled = isDidFieldsOrPropertyCountChangedCheckDisabled;
            EnableExperimentalAddedFieldsSupport = enableExperimentalAddedFieldsSupport;
        }
    }
}
#endif