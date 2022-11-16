using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QuickCodeIteration.Scripts.Runtime
{
    public class AssemblyChangesLoader
    {
        public static void DynamicallyUpdateMethodsForCreatedAssembly(string fullFilePath, Assembly dynamicallyLoadedAssemblyWithUpdates)
        {
            //TODO: how to unload previously generated assembly?
            var allTypesInNonDynamicGeneratedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.GetCustomAttributes<DynamicallyCreatedAssemblyAttribute>().Any())
                .SelectMany(a => a.GetTypes())
                .ToList();

            var excludeMethodsDefinedOnTypes = new List<Type>
            {
                typeof(MonoBehaviour),
                typeof(Behaviour),
                typeof(UnityEngine.Object),
                typeof(Component),
                typeof(System.Object)
            }; //TODO: move out and possibly define a way to exclude all non-client created code? as this will crash editor

            const BindingFlags ALL_METHODS_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
                                                           BindingFlags.Static | BindingFlags.Instance |
                                                           BindingFlags.FlattenHierarchy; //TODO: move out

            foreach (var createdType in dynamicallyLoadedAssemblyWithUpdates.GetTypes()
                         .Where(t => t.IsClass
                                     && !typeof(Delegate).IsAssignableFrom(t) //don't redirect delegates
                         )
                    )
            {
                var matchingTypeInExistingAssemblies = allTypesInNonDynamicGeneratedAssemblies.SingleOrDefault(t => t.FullName == createdType.FullName);
                if (matchingTypeInExistingAssemblies != null)
                {
                    var allMethodsInExistingType = matchingTypeInExistingAssemblies.GetMethods(ALL_METHODS_BINDING_FLAGS)
                        .Where(m => !excludeMethodsDefinedOnTypes.Contains(m.DeclaringType))
                        .ToList();
                    foreach (var createdTypeMethodToUpdate in createdType.GetMethods(ALL_METHODS_BINDING_FLAGS)
                                 .Where(m => !excludeMethodsDefinedOnTypes.Contains(m.DeclaringType)))
                    {
                        var matchingMethodInExistingType = allMethodsInExistingType.SingleOrDefault(m => m.FullDescription() == createdTypeMethodToUpdate.FullDescription());
                        if (matchingMethodInExistingType != null)
                        {
                            Memory.DetourMethod(matchingMethodInExistingType, createdTypeMethodToUpdate);
                        }
                        else
                        {
                            Debug.LogWarning($"Method: {createdTypeMethodToUpdate.FullDescription()} does not exist in initially compiled type: {matchingTypeInExistingAssemblies.FullName}. " +
                                             $"Adding new methods at runtime is not supported. Make sure to add method before initial compilation.");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"Unable to find existing type for: {createdType.FullName} from file: '{fullFilePath}'");
                }
            }
        }
    }
    
    
    [AttributeUsage(AttributeTargets.Assembly)]
    public class DynamicallyCreatedAssemblyAttribute : Attribute
    {
        public string GenerationIdentifier { get; }

        public DynamicallyCreatedAssemblyAttribute(string generationIdentifier)
        {
            GenerationIdentifier = generationIdentifier;
        }
    }
}