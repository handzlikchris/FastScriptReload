using System;
using System.Reflection;
using System.Linq.Expressions;
using MonoMod.Utils;
using System.Collections.Generic;
using MonoMod.RuntimeDetour.Platforms;
using Mono.Cecil.Cil;
using System.Threading;
using Mono.Cecil;
using System.Globalization;

namespace MonoMod.RuntimeDetour {
#if !MONOMOD_INTERNAL
    public
#endif
    static class DetourHelper {

        private static readonly object _RuntimeLock = new object();
        private static bool _RuntimeInit = false;
        private static IDetourRuntimePlatform _Runtime;
        public static IDetourRuntimePlatform Runtime {
            get {
                if (_Runtime != null)
                    return _Runtime;

                lock (_RuntimeLock) {
                    if (_Runtime != null)
                        return _Runtime;

                    if (_RuntimeInit)
                        return null;
                    _RuntimeInit = true;

                    if (ReflectionHelper.IsMono) {
                        _Runtime = new DetourRuntimeMonoPlatform();
                    } else if (ReflectionHelper.IsCore) {
                        _Runtime = DetourRuntimeNETCorePlatform.Create();
                    } else {
                        _Runtime = new DetourRuntimeNETPlatform();
                    }

                    return _Runtime;
                }
            }
            set => _Runtime = value;
        }

        private static readonly object _NativeLock = new object();
        private static bool _NativeInit = false;
        private static IDetourNativePlatform _Native;
        public static IDetourNativePlatform Native {
            get {
                if (_Native != null)
                    return _Native;

                lock (_NativeLock) {
                    if (_Native != null)
                        return _Native;

                    if (_NativeInit)
                        return null;
                    _NativeInit = true;

                    IDetourNativePlatform native;

                    if (PlatformHelper.Is(Platform.ARM)) {
                        native = new DetourNativeARMPlatform();
                    } else {
                        native = new DetourNativeX86Platform();
                    }

                    if (PlatformHelper.Is(Platform.Windows)) {
                        return _Native = new DetourNativeWindowsPlatform(native);
                    }

                    if (ReflectionHelper.IsMono) {
                        try {
                            // It's prefixed with lib on every platform.
                            return _Native = new DetourNativeMonoPlatform(native, $"libmonosgen-2.0.{PlatformHelper.LibrarySuffix}");
                        } catch {
                            // Fall back to another native platform wrapper.
                        }
                    } else {
                        // .NET Core currently doesn't contain any meaningful built-in wrappers.
                    }

                    // MonoPosixHelper is available outside of Unix and even outside of Mono.
                    // ... yet the available version might be incompatible with the current runtime for unknown reasons.
                    string isMonoPosixPreferred = Environment.GetEnvironmentVariable("MONOMOD_RUNTIMEDETOUR_MONOPOSIXHELPER");
                    if ((ReflectionHelper.IsMono && isMonoPosixPreferred != "0") || isMonoPosixPreferred == "1") {
                        try {
                            return _Native = new DetourNativeMonoPosixPlatform(native);
                        } catch {
                            // Good job, your copy of Mono doesn't ship with MonoPosixHelper.
                            // https://www.youtube.com/watch?v=l60MnDJklnM (Michael Jordan: Stop It, Get Some Help)
                        }
                    }

                    // Might as well try libc...
                    try {
                        return _Native = new DetourNativeLibcPlatform(native);
                    } catch {
                        // Oh well.
                    }

                    return native;
                }
            }
            set => _Native = value;
        }

        #region Interface extension methods

        public static void MakeWritable(this IDetourNativePlatform plat, NativeDetourData detour) => plat.MakeWritable(detour.Method, detour.Size);
        public static void MakeExecutable(this IDetourNativePlatform plat, NativeDetourData detour) => plat.MakeExecutable(detour.Method, detour.Size);
        public static void FlushICache(this IDetourNativePlatform plat, NativeDetourData detour) => plat.FlushICache(detour.Method, detour.Size);

        #endregion

        #region Native helpers

        /// <summary>
        /// Write the given value at the address to + offs, afterwards advancing offs by sizeof(byte).
        /// </summary>
        public static unsafe void Write(this IntPtr to, ref int offs, byte value) {
            *((byte*) ((long) to + offs)) = value;
            offs += 1;
        }
        /// <summary>
        /// Write the given value at the address to + offs, afterwards advancing offs by sizeof(ushort).
        /// </summary>
        public static unsafe void Write(this IntPtr to, ref int offs, ushort value) {
            *((ushort*) ((long) to + offs)) = value;
            offs += 2;
        }
        /// <summary>
        /// Write the given value at the address to + offs, afterwards advancing offs by sizeof(ushort).
        /// </summary>
        public static unsafe void Write(this IntPtr to, ref int offs, uint value) {
            *((uint*) ((long) to + offs)) = value;
            offs += 4;
        }
        /// <summary>
        /// Write the given value at the address to + offs, afterwards advancing offs by sizeof(ulong).
        /// </summary>
        public static unsafe void Write(this IntPtr to, ref int offs, ulong value) {
            *((ulong*) ((long) to + offs)) = value;
            offs += 8;
        }

        #endregion

        #region Method-related helpers

        public static MethodBase GetIdentifiable(this MethodBase method)
            => Runtime.GetIdentifiable(method);

        public static IntPtr GetNativeStart(this MethodBase method)
            => Runtime.GetNativeStart(method);
        public static IntPtr GetNativeStart(this Delegate method)
            => method.Method.GetNativeStart();
        public static IntPtr GetNativeStart(this Expression method)
            => ((MethodCallExpression) method).Method.GetNativeStart();

        public static MethodInfo CreateILCopy(this MethodBase method)
            => Runtime.CreateCopy(method);
        public static bool TryCreateILCopy(this MethodBase method, out MethodInfo dm)
            => Runtime.TryCreateCopy(method, out dm);

        public static T Pin<T>(this T method) where T : MethodBase {
            Runtime.Pin(method);
            return method;
        }

        public static T Unpin<T>(this T method) where T : MethodBase {
            Runtime.Unpin(method);
            return method;
        }

        #endregion

        #region DynamicMethod generation helpers

        /// <summary>
        /// Generate a DynamicMethod to easily call the given native function from another DynamicMethod.
        /// </summary>
        /// <param name="target">The pointer to the native function to call.</param>
        /// <param name="signature">A MethodBase with the target function's signature.</param>
        /// <returns>The detoured DynamicMethod.</returns>
        public static MethodInfo GenerateNativeProxy(IntPtr target, MethodBase signature) {
            Type returnType = (signature as MethodInfo)?.ReturnType ?? typeof(void);

            ParameterInfo[] args = signature.GetParameters();
            Type[] argTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                argTypes[i] = args[i].ParameterType;

            MethodInfo dm;
            using (DynamicMethodDefinition dmd = new DynamicMethodDefinition(
                $"Native<{((long) target).ToString("X16", CultureInfo.InvariantCulture)}>",
                returnType, argTypes
            ))
                dm = dmd.StubCriticalDetour().Generate().Pin();

            // Detour the new DynamicMethod into the target.
            NativeDetourData detour = Native.Create(dm.GetNativeStart(), target);
            Native.MakeWritable(detour);
            Native.Apply(detour);
            Native.MakeExecutable(detour);
            Native.FlushICache(detour);
            Native.Free(detour);

            return dm;
        }

        // Used in EmitDetourApply.
        private static NativeDetourData ToNativeDetourData(IntPtr method, IntPtr target, uint size, byte type, IntPtr extra)
            => new NativeDetourData {
                Method = method,
                Target = target,
                Size = size,
                Type = type,
                Extra = extra
            };

        private static readonly FieldInfo _f_Native = typeof(DetourHelper).GetField("_Native", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo _m_ToNativeDetourData = typeof(DetourHelper).GetMethod("ToNativeDetourData", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo _m_Copy = typeof(IDetourNativePlatform).GetMethod("Copy");
        private static readonly MethodInfo _m_Apply = typeof(IDetourNativePlatform).GetMethod("Apply");
        private static readonly ConstructorInfo _ctor_Exception = typeof(Exception).GetConstructor(new Type[] { typeof(string) });

        /// <summary>
        /// Fill the DynamicMethodDefinition with a throw.
        /// </summary>
        public static DynamicMethodDefinition StubCriticalDetour(this DynamicMethodDefinition dm) {
            ILProcessor il = dm.GetILProcessor();
            ModuleDefinition ilModule = il.Body.Method.Module;
            for (int i = 0; i < 32; i++) {
                // Prevent mono from inlining the DynamicMethod.
                il.Emit(OpCodes.Nop);
            }
            il.Emit(OpCodes.Ldstr, $"{dm.Definition.Name} should've been detoured!");
            il.Emit(OpCodes.Newobj, ilModule.ImportReference(_ctor_Exception));
            il.Emit(OpCodes.Throw);
            return dm;
        }

        /// <summary>
        /// Emit a call to DetourManager.Native.Copy using the given parameters.
        /// </summary>
        public static void EmitDetourCopy(this ILProcessor il, IntPtr src, IntPtr dst, byte type) {
            ModuleDefinition ilModule = il.Body.Method.Module;

            // Load NativePlatform instance.
            il.Emit(OpCodes.Ldsfld, ilModule.ImportReference(_f_Native));

            // Fill stack with src, dst, type
            il.Emit(OpCodes.Ldc_I8, (long) src);
            il.Emit(OpCodes.Conv_I);
            il.Emit(OpCodes.Ldc_I8, (long) dst);
            il.Emit(OpCodes.Conv_I);
            il.Emit(OpCodes.Ldc_I4, (int) type);
            il.Emit(OpCodes.Conv_U1);

            // Copy.
            il.Emit(OpCodes.Callvirt, ilModule.ImportReference(_m_Copy));
        }

        /// <summary>
        /// Emit a call to DetourManager.Native.Apply using a copy of the given data.
        /// </summary>
        public static void EmitDetourApply(this ILProcessor il, NativeDetourData data) {
            ModuleDefinition ilModule = il.Body.Method.Module;

            // Load NativePlatform instance.
            il.Emit(OpCodes.Ldsfld, ilModule.ImportReference(_f_Native));

            // Fill stack with data values.
            il.Emit(OpCodes.Ldc_I8, (long) data.Method);
            il.Emit(OpCodes.Conv_I);
            il.Emit(OpCodes.Ldc_I8, (long) data.Target);
            il.Emit(OpCodes.Conv_I);
            il.Emit(OpCodes.Ldc_I4, (int) data.Size);
            il.Emit(OpCodes.Ldc_I4, (int) data.Type);
            il.Emit(OpCodes.Conv_U1);
            il.Emit(OpCodes.Ldc_I8, (long) data.Extra);
            il.Emit(OpCodes.Conv_I);

            // Put values in stack into NativeDetourData.
            il.Emit(OpCodes.Call, ilModule.ImportReference(_m_ToNativeDetourData));

            // Apply.
            il.Emit(OpCodes.Callvirt, ilModule.ImportReference(_m_Apply));
        }

        #endregion

    }
}
