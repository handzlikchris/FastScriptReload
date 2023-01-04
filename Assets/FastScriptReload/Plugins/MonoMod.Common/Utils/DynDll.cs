using System.Reflection;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Linq;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    static partial class DynDll {

        /// <summary>
        /// Allows you to remap library paths / names and specify loading flags. Useful for cross-platform compatibility. Applies only to DynDll.
        /// </summary>
        public static Dictionary<string, List<DynDllMapping>> Mappings = new Dictionary<string, List<DynDllMapping>>();

        /// <summary>
        /// Extension method wrapping Marshal.GetDelegateForFunctionPointer
        /// </summary>
        public static T AsDelegate<T>(this IntPtr s) where T : class {
#pragma warning disable CS0618 // Type or member is obsolete
            return Marshal.GetDelegateForFunctionPointer(s, typeof(T)) as T;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Fill all static delegate fields with the DynDllImport attribute.
        /// Call this early on in the static constructor.
        /// </summary>
        /// <param name="type">The type containing the DynDllImport delegate fields.</param>
        /// <param name="mappings">Any optional mappings similar to the static mappings.</param>
        public static void ResolveDynDllImports(this Type type, Dictionary<string, List<DynDllMapping>> mappings = null)
            => InternalResolveDynDllImports(type, null, mappings);

        /// <summary>
        /// Fill all instance delegate fields with the DynDllImport attribute.
        /// Call this early on in the constructor.
        /// </summary>
        /// <param name="instance">An instance of a type containing the DynDllImport delegate fields.</param>
        /// <param name="mappings">Any optional mappings similar to the static mappings.</param>
        public static void ResolveDynDllImports(object instance, Dictionary<string, List<DynDllMapping>> mappings = null)
            => InternalResolveDynDllImports(instance.GetType(), instance, mappings);

        private static void InternalResolveDynDllImports(Type type, object instance, Dictionary<string, List<DynDllMapping>> mappings) {
            BindingFlags fieldFlags = BindingFlags.Public | BindingFlags.NonPublic;
            if (instance == null)
                fieldFlags |= BindingFlags.Static;
            else
                fieldFlags |= BindingFlags.Instance;

            foreach (FieldInfo field in type.GetFields(fieldFlags)) {
                bool found = true;

                foreach (DynDllImportAttribute attrib in field.GetCustomAttributes(typeof(DynDllImportAttribute), true)) {
                    found = false;

                    IntPtr libraryPtr = IntPtr.Zero;

                    if (mappings != null && mappings.TryGetValue(attrib.LibraryName, out List<DynDllMapping> mappingList)) {
                        bool mappingFound = false;

                        foreach (var mapping in mappingList) {
                            if (TryOpenLibrary(mapping.LibraryName, out libraryPtr, true, mapping.Flags)) {
                                mappingFound = true;
                                break;
                            }
                        }

                        if (!mappingFound)
                            continue;
                    } else {
                        if (!TryOpenLibrary(attrib.LibraryName, out libraryPtr))
                            continue;
                    }


                    foreach (string entryPoint in attrib.EntryPoints.Concat(new[] { field.Name, field.FieldType.Name })) {
                        if (!libraryPtr.TryGetFunction(entryPoint, out IntPtr functionPtr))
                            continue;

#pragma warning disable CS0618 // Type or member is obsolete
                        field.SetValue(instance, Marshal.GetDelegateForFunctionPointer(functionPtr, field.FieldType));
#pragma warning restore CS0618 // Type or member is obsolete

                        found = true;
                        break;
                    }

                    if (found)
                        break;
                }

                if (!found)
                    throw new EntryPointNotFoundException($"No matching entry point found for {field.Name} in {field.DeclaringType.FullName}");
            }
        }

        public static class DlopenFlags {
            public const int RTLD_LAZY = 0x0001;
            public const int RTLD_NOW = 0x0002;
            public const int RTLD_LOCAL = 0x0000;
            public const int RTLD_GLOBAL = 0x0100;
        }
    }

    /// <summary>
    /// Similar to DllImport, but requires you to run typeof(DeclaringType).ResolveDynDllImports();
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class DynDllImportAttribute : Attribute {
        /// <summary>
        /// The library or library alias to use.
        /// </summary>
        public string LibraryName { get; set; }

        /// <summary>
        /// A list of possible entrypoints that the function can be resolved to. Implicitly includes the field name and delegate name.
        /// </summary>
        public string[] EntryPoints { get; set; }

        /// <param name="libraryName">The library or library alias to use.</param>
        /// <param name="entryPoints">A list of possible entrypoints that the function can be resolved to. Implicitly includes the field name and delegate name.</param>
        public DynDllImportAttribute(string libraryName, params string[] entryPoints) {
            LibraryName = libraryName;
            EntryPoints = entryPoints;
        }
    }

    /// <summary>
    /// A mapping entry, to be used by <see cref="DynDllImportAttribute"/>.
    /// </summary>
    public sealed class DynDllMapping {
        /// <summary>
        /// The name as which the library will be resolved as. Useful to remap libraries or to provide full paths.
        /// </summary>
        public string LibraryName { get; set; }

        /// <summary>
        /// Platform-dependent loading flags.
        /// </summary>
        public int? Flags { get; set; }

        /// <param name="libraryName">The name as which the library will be resolved as. Useful to remap libraries or to provide full paths.</param>
        /// <param name="flags">Platform-dependent loading flags.</param>
		public DynDllMapping(string libraryName, int? flags = null) {
            LibraryName = libraryName ?? throw new ArgumentNullException(nameof(libraryName));
            Flags = flags;
        }

        public static implicit operator DynDllMapping(string libraryName) {
            return new DynDllMapping(libraryName);
        }
    }
}
