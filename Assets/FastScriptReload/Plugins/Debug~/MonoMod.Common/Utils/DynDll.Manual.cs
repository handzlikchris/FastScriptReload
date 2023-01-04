#if !NETCOREAPP3_0_OR_GREATER
#pragma warning disable IDE1006 // Naming Styles
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

        #region kernel32 imports

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hLibModule);
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        #endregion

        #region dl imports

        [DllImport("dl", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dl_dlopen(string filename, int flags);
        [DllImport("dl", EntryPoint = "dlclose", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool dl_dlclose(IntPtr handle);
        [DllImport("dl", EntryPoint = "dlsym", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dl_dlsym(IntPtr handle, string symbol);
        [DllImport("dl", EntryPoint = "dlerror", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dl_dlerror();

        #endregion

        #region libdl.so.2 imports

        [DllImport("libdl.so.2", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dl2_dlopen(string filename, int flags);
        [DllImport("libdl.so.2", EntryPoint = "dlclose", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool dl2_dlclose(IntPtr handle);
        [DllImport("libdl.so.2", EntryPoint = "dlsym", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dl2_dlsym(IntPtr handle, string symbol);
        [DllImport("libdl.so.2", EntryPoint = "dlerror", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dl2_dlerror();

        #endregion

        #region dl wrappers

        private static int dlVersion = 1;

        private static IntPtr dlopen(string filename, int flags) {
            while (true) {
                try {
                    switch (dlVersion) {
                        case 1:
                            return dl2_dlopen(filename, flags);

                        case 0:
                        default:
                            return dl_dlopen(filename, flags);
                    }
                } catch (DllNotFoundException) when (dlVersion > 0) {
                    dlVersion--;
                }
            }
        }

        private static bool dlclose(IntPtr handle) {
            while (true) {
                try {
                    switch (dlVersion) {
                        case 1:
                            return dl2_dlclose(handle);

                        case 0:
                        default:
                            return dl_dlclose(handle);
                    }
                } catch (DllNotFoundException) when (dlVersion > 0) {
                    dlVersion--;
                }
            }
        }

        private static IntPtr dlsym(IntPtr handle, string symbol) {
            while (true) {
                try {
                    switch (dlVersion) {
                        case 1:
                            return dl2_dlsym(handle, symbol);

                        case 0:
                        default:
                            return dl_dlsym(handle, symbol);
                    }
                } catch (DllNotFoundException) when (dlVersion > 0) {
                    dlVersion--;
                }
            }
        }

        private static IntPtr dlerror() {
            while (true) {
                try {
                    switch (dlVersion) {
                        case 1:
                            return dl2_dlerror();

                        case 0:
                        default:
                            return dl_dlerror();
                    }
                } catch (DllNotFoundException) when (dlVersion > 0) {
                    dlVersion--;
                }
            }
        }

        #endregion

        static DynDll() {
            // Run a dummy dlerror to resolve it so that it won't interfere with the first call
            if (!PlatformHelper.Is(Platform.Windows))
                dlerror();
        }

        private static bool CheckError(out Exception exception) {
            if (PlatformHelper.Is(Platform.Windows)) {
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode != 0) {
                    exception = new Win32Exception(errorCode);
                    return false;
                }
            } else {
                IntPtr errorCode = dlerror();
                if (errorCode != IntPtr.Zero) {
                    exception = new Win32Exception(Marshal.PtrToStringAnsi(errorCode));
                    return false;
                }
            }

            exception = null;
            return true;
        }

        /// <summary>
        /// Open a given library and get its handle.
        /// </summary>
        /// <param name="name">The library name.</param>
        /// <param name="skipMapping">Whether to skip using the mapping or not.</param>
        /// <param name="flags">Any optional platform-specific flags.</param>
        /// <returns>The library handle.</returns>
        public static IntPtr OpenLibrary(string name, bool skipMapping = false, int? flags = null) {
            if (!InternalTryOpenLibrary(name, out var libraryPtr, skipMapping, flags))
                throw new DllNotFoundException($"Unable to load library '{name}'");

            if (!CheckError(out var exception))
                throw exception;

            return libraryPtr;
        }

        /// <summary>
        /// Try to open a given library and get its handle.
        /// </summary>
        /// <param name="name">The library name.</param>
		/// <param name="libraryPtr">The library handle, or null if it failed loading.</param>
        /// <param name="skipMapping">Whether to skip using the mapping or not.</param>
        /// <param name="flags">Any optional platform-specific flags.</param>
        /// <returns>True if the handle was obtained, false otherwise.</returns>
        public static bool TryOpenLibrary(string name, out IntPtr libraryPtr, bool skipMapping = false, int? flags = null) {
            return InternalTryOpenLibrary(name, out libraryPtr, skipMapping, flags) || CheckError(out _);
        }

        private static bool InternalTryOpenLibrary(string name, out IntPtr libraryPtr, bool skipMapping, int? flags) {
            if (name != null && !skipMapping && Mappings.TryGetValue(name, out List<DynDllMapping> mappingList)) {
                foreach (var mapping in mappingList) {
                    if (InternalTryOpenLibrary(mapping.LibraryName, out libraryPtr, true, mapping.Flags))
                        return true;
                }

                libraryPtr = IntPtr.Zero;
                return true;
            }

            if (PlatformHelper.Is(Platform.Windows)) {
                libraryPtr = name == null
                    ? GetModuleHandle(name)
                    : LoadLibrary(name);
            } else {
                int _flags = flags ?? (DlopenFlags.RTLD_NOW | DlopenFlags.RTLD_GLOBAL); // Default should match LoadLibrary.

                libraryPtr = dlopen(name, _flags);

                if (libraryPtr == IntPtr.Zero && File.Exists(name))
                    libraryPtr = dlopen(Path.GetFullPath(name), _flags);
            }

            return libraryPtr != IntPtr.Zero;
        }

        /// <summary>
        /// Release a library handle obtained via OpenLibrary. Don't release the result of OpenLibrary(null)!
        /// </summary>
        /// <param name="lib">The library handle.</param>
        public static bool CloseLibrary(IntPtr lib) {
            if (PlatformHelper.Is(Platform.Windows))
                CloseLibrary(lib);
            else
                dlclose(lib);

            return CheckError(out _);
        }

        /// <summary>
        /// Get a function pointer for a function in the given library.
        /// </summary>
        /// <param name="libraryPtr">The library handle.</param>
        /// <param name="name">The function name.</param>
        /// <returns>The function pointer.</returns>
        public static IntPtr GetFunction(this IntPtr libraryPtr, string name) {
            if (!InternalTryGetFunction(libraryPtr, name, out var functionPtr))
                throw new MissingMethodException($"Unable to load function '{name}'");

            if (!CheckError(out var exception))
                throw exception;

            return functionPtr;
        }

        /// <summary>
        /// Get a function pointer for a function in the given library.
        /// </summary>
        /// <param name="libraryPtr">The library handle.</param>
        /// <param name="name">The function name.</param>
        /// <param name="functionPtr">The function pointer, or null if it wasn't found.</param>
        /// <returns>True if the function pointer was obtained, false otherwise.</returns>
        public static bool TryGetFunction(this IntPtr libraryPtr, string name, out IntPtr functionPtr) {
            return InternalTryGetFunction(libraryPtr, name, out functionPtr) || CheckError(out _);
        }

        private static bool InternalTryGetFunction(IntPtr libraryPtr, string name, out IntPtr functionPtr) {
            if (libraryPtr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(libraryPtr));

            functionPtr = PlatformHelper.Is(Platform.Windows)
                ? GetProcAddress(libraryPtr, name)
                : dlsym(libraryPtr, name);

            return functionPtr != IntPtr.Zero;
        }

    }
}
#endif