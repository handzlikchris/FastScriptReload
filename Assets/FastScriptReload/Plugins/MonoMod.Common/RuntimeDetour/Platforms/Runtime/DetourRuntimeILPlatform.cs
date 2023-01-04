using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.Utils;
using System.Linq;
using Mono.Cecil.Cil;
using System.Threading;
#if !NET35
using System.Collections.Concurrent;
#endif

namespace MonoMod.RuntimeDetour.Platforms {
#if !MONOMOD_INTERNAL
    public
#endif
    abstract class DetourRuntimeILPlatform : IDetourRuntimePlatform {
        protected abstract RuntimeMethodHandle GetMethodHandle(MethodBase method);

        protected GlueThiscallStructRetPtrOrder GlueThiscallStructRetPtr;
        protected GlueThiscallStructRetPtrOrder GlueThiscallInStructRetPtr;

        // The following dicts are needed to prevent the GC from collecting DynamicMethods without any visible references.
        // PinnedHandles is also used in certain situations as a fallback when getting a method from a handle may not work normally.
#if NET35
        protected Dictionary<MethodBase, PrivateMethodPin> PinnedMethods = new Dictionary<MethodBase, PrivateMethodPin>();
        protected Dictionary<RuntimeMethodHandle, PrivateMethodPin> PinnedHandles = new Dictionary<RuntimeMethodHandle, PrivateMethodPin>();
#else
        protected ConcurrentDictionary<MethodBase, PrivateMethodPin> PinnedMethods = new ConcurrentDictionary<MethodBase, PrivateMethodPin>();
        protected ConcurrentDictionary<RuntimeMethodHandle, PrivateMethodPin> PinnedHandles = new ConcurrentDictionary<RuntimeMethodHandle, PrivateMethodPin>();
#endif

        public abstract bool OnMethodCompiledWillBeCalled { get; }
        public abstract event OnMethodCompiledEvent OnMethodCompiled;

        private IntPtr ReferenceNonDynamicPoolPtr;
        private IntPtr ReferenceDynamicPoolPtr;

        public DetourRuntimeILPlatform() {
            // Perform a selftest if this runtime requires special handling for instance methods returning structs.
            // This is documented behavior for coreclr, but affects other runtimes (i.e. mono) as well!
            // Specifically, this should affect all __thiscalls

            // Use reflection to make sure that the selftest isn't optimized away.
            // Delegates are quite reliable for this job.

            MethodInfo selftestGetRefPtr = typeof(DetourRuntimeILPlatform).GetMethod("_SelftestGetRefPtr", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo selftestGetRefPtrHook = typeof(DetourRuntimeILPlatform).GetMethod("_SelftestGetRefPtrHook", BindingFlags.NonPublic | BindingFlags.Static);
            _HookSelftest(selftestGetRefPtr, selftestGetRefPtrHook);

            IntPtr selfPtr = ((Func<IntPtr>) Delegate.CreateDelegate(typeof(Func<IntPtr>), this, selftestGetRefPtr))();

            MethodInfo selftestGetStruct = typeof(DetourRuntimeILPlatform).GetMethod("_SelftestGetStruct", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo selftestGetStructHook = typeof(DetourRuntimeILPlatform).GetMethod("_SelftestGetStructHook", BindingFlags.NonPublic | BindingFlags.Static);
            _HookSelftest(selftestGetStruct, selftestGetStructHook);

            unsafe {
                fixed (GlueThiscallStructRetPtrOrder* orderPtr = &GlueThiscallStructRetPtr) {
                    ((Func<IntPtr, IntPtr, IntPtr, _SelftestStruct>) Delegate.CreateDelegate(typeof(Func<IntPtr, IntPtr, IntPtr, _SelftestStruct>), this, selftestGetStruct))((IntPtr) orderPtr, (IntPtr) orderPtr, selfPtr);
                }
            }

            MethodInfo selftestGetInStruct = typeof(_SelftestStruct).GetMethod("_SelftestGetInStruct", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo selftestGetInStructHook = typeof(DetourRuntimeILPlatform).GetMethod("_SelftestGetInStructHook", BindingFlags.NonPublic | BindingFlags.Static);
            _HookSelftest(selftestGetInStruct, selftestGetInStructHook);

            unsafe {
                fixed (GlueThiscallStructRetPtrOrder* orderPtr = &GlueThiscallInStructRetPtr) {
                    object box = new _SelftestStruct();
                    *orderPtr = (GlueThiscallStructRetPtrOrder) ((Func<short>) Delegate.CreateDelegate(typeof(Func<short>), box, selftestGetInStruct))();
                    if ((int) *orderPtr == -1)
                        throw new Exception("_SelftestGetInStruct failed!");
                }
            }

            // Get some reference (not reference as in ref but reference as in "to compare against") dyn and non-dyn method pointers.
            Pin(selftestGetRefPtr);
            ReferenceNonDynamicPoolPtr = GetNativeStart(selftestGetRefPtr);

            if (DynamicMethodDefinition.IsDynamicILAvailable) {
                MethodBase scratch;
                using (DynamicMethodDefinition copy = new DynamicMethodDefinition(_MemAllocScratchDummy)) {
                    copy.Name = $"MemAllocScratch<Reference>";
                    scratch = DMDEmitDynamicMethodGenerator.Generate(copy);
                }
                Pin(scratch);
                ReferenceDynamicPoolPtr = GetNativeStart(scratch);
            }
        }

        private void _HookSelftest(MethodInfo from, MethodInfo to) {
            Pin(from);
            Pin(to);
            NativeDetourData detour = DetourHelper.Native.Create(
                GetNativeStart(from),
                GetNativeStart(to),
                null
            );
            DetourHelper.Native.MakeWritable(detour);
            DetourHelper.Native.Apply(detour);
            DetourHelper.Native.MakeExecutable(detour);
            DetourHelper.Native.FlushICache(detour);
            DetourHelper.Native.Free(detour);
            // No need to undo the detour.
        }

#region Selftests

#region Selftest: Get reference ptr

        [MethodImpl(MethodImplOptions.NoInlining)]
        private IntPtr _SelftestGetRefPtr() {
            Console.Error.WriteLine("If you're reading this, the MonoMod.RuntimeDetour selftest failed.");
            throw new Exception("This method should've been detoured!");
        }

        private static unsafe IntPtr _SelftestGetRefPtrHook(IntPtr self) {
            // This is only needed to obtain a raw IntPtr to a reference object.
            return self;
        }

#endregion

#region Selftest: Struct

        // In 32-bit envs, struct must be 3 or 4+ bytes big.
        // In 64-bit envs, struct must be 3, 5, 6, 7 or 9+ bytes big.
#pragma warning disable CS0169
        private struct _SelftestStruct {
            private readonly short Value;
            private readonly byte E1, E2, E3;
            [MethodImpl(MethodImplOptions.NoInlining)]
            public short _SelftestGetInStruct() {
                Console.Error.WriteLine("If you're reading this, the MonoMod.RuntimeDetour selftest failed.");
                return -1;
            }
        }
#pragma warning restore CS0169

        [MethodImpl(MethodImplOptions.NoInlining)]
        private _SelftestStruct _SelftestGetStruct(IntPtr x, IntPtr y, IntPtr thisPtr) {
            Console.Error.WriteLine("If you're reading this, the MonoMod.RuntimeDetour selftest failed.");
            throw new Exception("_SelftestGetStruct failed!");
        }

        private static unsafe void _SelftestGetStructHook(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e) {
            // Normally, a = this, b = x, c = y, d = thisPtr, e = garbage

            // For the general selftest, x must be equal to y.
            // If b != c, b is probably pointing to the return buffer or this.
            if (b == c) {
                // Original order.
                *((GlueThiscallStructRetPtrOrder*) b) = GlueThiscallStructRetPtrOrder.Original;

            } else if (b == e) {
                // For mono in Unity 5.6.X, a = __ret, b = this, c = x, d = y, e = thisPtr
                *((GlueThiscallStructRetPtrOrder*) c) = GlueThiscallStructRetPtrOrder.RetThisArgs;

            } else {
                // For coreclr x64 __thiscall, a = this, b = __ret, c = x, d = y, e = thisPtr
                *((GlueThiscallStructRetPtrOrder*) c) = GlueThiscallStructRetPtrOrder.ThisRetArgs;

            }
        }

        private static unsafe short _SelftestGetInStructHook(IntPtr a) {
            // I don't know what's supposed to be normal and what's special anymore. -ade
            *(short*) a = (short) GlueThiscallStructRetPtrOrder.RetThisArgs;
            return (short) GlueThiscallStructRetPtrOrder.Original;
        }

        #endregion

        #endregion

        protected virtual IntPtr GetFunctionPointer(MethodBase method, RuntimeMethodHandle handle)
            => handle.GetFunctionPointer();

        protected virtual void PrepareMethod(MethodBase method, RuntimeMethodHandle handle)
            => RuntimeHelpers.PrepareMethod(handle);

        protected virtual void PrepareMethod(MethodBase method, RuntimeMethodHandle handle, RuntimeTypeHandle[] instantiation)
            => RuntimeHelpers.PrepareMethod(handle, instantiation);

        protected virtual void DisableInlining(MethodBase method, RuntimeMethodHandle handle) {
            // no-op. Not supported on all platforms, but throwing an exception doesn't make sense.
        }

        public virtual MethodBase GetIdentifiable(MethodBase method) {
#if NET35
            lock (PinnedMethods)
                return PinnedHandles.TryGetValue(GetMethodHandle(method), out PrivateMethodPin pin) ? pin.Pin.Method : method;
#else
            return PinnedHandles.TryGetValue(GetMethodHandle(method), out PrivateMethodPin pin) ? pin.Pin.Method : method;
#endif
        }

        public virtual MethodPinInfo GetPin(MethodBase method) {
#if NET35
            lock (PinnedMethods)
                return PinnedMethods.TryGetValue(method, out PrivateMethodPin pin) ? pin.Pin : default;
#else
            return PinnedMethods.TryGetValue(method, out PrivateMethodPin pin) ? pin.Pin : default;
#endif
        }

        public virtual MethodPinInfo GetPin(RuntimeMethodHandle handle) {
#if NET35
            lock (PinnedMethods)
                return PinnedHandles.TryGetValue(handle, out PrivateMethodPin pin) ? pin.Pin : default;
#else
            return PinnedHandles.TryGetValue(handle, out PrivateMethodPin pin) ? pin.Pin : default;
#endif
        }

        public virtual MethodPinInfo[] GetPins() {
#if NET35
            lock (PinnedMethods)
                return PinnedHandles.Values.Select(p => p.Pin).ToArray();
#else
            return PinnedHandles.Values.ToArray().Select(p => p.Pin).ToArray();
#endif
        }

        public virtual IntPtr GetNativeStart(MethodBase method) {
            method = GetIdentifiable(method);
            bool pinGot;
            PrivateMethodPin pin;
#if NET35
            lock (PinnedMethods)
#endif
            {
                pinGot = PinnedMethods.TryGetValue(method, out pin);
            }
            if (pinGot)
                return GetFunctionPointer(method, pin.Pin.Handle);
            return GetFunctionPointer(method, GetMethodHandle(method));
        }

        public virtual void Pin(MethodBase method) {
            method = GetIdentifiable(method);
#if NET35
            lock (PinnedMethods) {
                if (PinnedMethods.TryGetValue(method, out PrivateMethodPin pin)) {
                    pin.Pin.Count++;
                    return;
                }

                MethodBase m = method;
                pin = new PrivateMethodPin();
                pin.Pin.Count = 1;

#else
            Interlocked.Increment(ref PinnedMethods.GetOrAdd(method, m => {
                PrivateMethodPin pin = new PrivateMethodPin();
#endif

                pin.Pin.Method = m;
                RuntimeMethodHandle handle = pin.Pin.Handle = GetMethodHandle(m);
                PinnedHandles[handle] = pin;

                DisableInlining(method, handle);
                if (method.DeclaringType?.IsGenericType ?? false) {
                    PrepareMethod(method, handle, method.DeclaringType.GetGenericArguments().Select(type => type.TypeHandle).ToArray());
                } else {
                    PrepareMethod(method, handle);
                }

#if !NET35
                return pin;
#endif
            }
#if !NET35
            ).Pin.Count);
#endif
        }

        public virtual void Unpin(MethodBase method) {
            method = GetIdentifiable(method);
#if NET35
            lock (PinnedMethods) {
                if (!PinnedMethods.TryGetValue(method, out PrivateMethodPin pin))
                    return;

                if (pin.Pin.Count <= 1) {
                    PinnedMethods.Remove(method);
                    PinnedHandles.Remove(pin.Pin.Handle);
                    return;
                }
                pin.Pin.Count--;
            }
#else
            if (!PinnedMethods.TryGetValue(method, out PrivateMethodPin pin))
                return;

            if (Interlocked.Decrement(ref pin.Pin.Count) <= 0) {
                PinnedMethods.TryRemove(method, out _);
                PinnedHandles.TryRemove(pin.Pin.Handle, out _);
            }
#endif
        }

        public MethodInfo CreateCopy(MethodBase method) {
            method = GetIdentifiable(method);
            if (method == null || (method.GetMethodImplementationFlags() & (MethodImplAttributes.OPTIL | MethodImplAttributes.Native | MethodImplAttributes.Runtime)) != 0) {
                throw new InvalidOperationException($"Uncopyable method: {method?.ToString() ?? "NULL"}");
            }

            using (DynamicMethodDefinition dmd = new DynamicMethodDefinition(method))
                return dmd.Generate();
        }

        public bool TryCreateCopy(MethodBase method, out MethodInfo dm) {
            method = GetIdentifiable(method);
            if (method == null || (method.GetMethodImplementationFlags() & (MethodImplAttributes.OPTIL | MethodImplAttributes.Native | MethodImplAttributes.Runtime)) != 0) {
                dm = null;
                return false;
            }

            try {
                dm = CreateCopy(method);
                return true;
            } catch {
                dm = null;
                return false;
            }
        }

        private static bool IsStruct(Type t) {
            if (t == null) return false;
            return t.IsValueType && !t.IsPrimitive && !t.IsEnum;
        }

        public MethodBase GetDetourTarget(MethodBase from, MethodBase to) {
            to = GetIdentifiable(to);

            MethodInfo dm = null;
            GlueThiscallStructRetPtrOrder glueThiscallStructRetPtr;

            if (from is MethodInfo fromInfo && !from.IsStatic &&
                to is MethodInfo toInfo && to.IsStatic &&
                fromInfo.ReturnType == toInfo.ReturnType &&
                IsStruct(fromInfo.ReturnType) &&
                (glueThiscallStructRetPtr =
                    IsStruct(from.DeclaringType) && from.GetParameters().Length == 0 ? GlueThiscallInStructRetPtr :
                    GlueThiscallStructRetPtr
                ) != GlueThiscallStructRetPtrOrder.Original) {

                int size = fromInfo.ReturnType.GetManagedSize();
                // This assumes that 8 bytes long structs work fine in 64-bit envs but not 32-bit envs.
                if (size == 3 || size == 5 || size == 6 || size == 7 || size > IntPtr.Size) {
                    Type thisType = from.GetThisParamType();
                    Type retType = fromInfo.ReturnType.MakeByRefType(); // Refs are shiny pointers.

                    int thisPos = 0;
                    int retPos = 1;

                    if (glueThiscallStructRetPtr == GlueThiscallStructRetPtrOrder.RetThisArgs) {
                        thisPos = 1;
                        retPos = 0;
                    }

                    List<Type> argTypes = new List<Type> {
                        thisType
                    };
                    argTypes.Insert(retPos, retType);

                    argTypes.AddRange(from.GetParameters().Select(p => p.ParameterType));

                    using (DynamicMethodDefinition dmd = new DynamicMethodDefinition(
                        $"Glue:ThiscallStructRetPtr<{from.GetID(simple: true)},{to.GetID(simple: true)}>",
                        typeof(void), argTypes.ToArray()
                    )) {
                        ILProcessor il = dmd.GetILProcessor();

                        // Load the return buffer address.
                        il.Emit(OpCodes.Ldarg, retPos);

                        // Invoke the target method with all remaining arguments.
                        {
                            il.Emit(OpCodes.Ldarg, thisPos);
                            for (int i = 2; i < argTypes.Count; i++)
                                il.Emit(OpCodes.Ldarg, i);
                            il.Emit(OpCodes.Call, il.Body.Method.Module.ImportReference(to));
                        }

                        // Store the returned object to the return buffer.
                        il.Emit(OpCodes.Stobj, il.Body.Method.Module.ImportReference(fromInfo.ReturnType));
                        il.Emit(OpCodes.Ret);

                        dm = dmd.Generate();
                    }
                }
            }

            return dm ?? to;
        }

        public uint TryMemAllocScratchCloseTo(IntPtr target, out IntPtr ptr, int size) {
            /* We can create a new method that is of the same type (dynamic or non-dynamic) as the target method,
             * assume that it will be closer to it than a new method of the opposite type, and use it as a pseudo-malloc.
             *
             * This is only (kinda?) documented on mono so far.
             * See https://www.mono-project.com/docs/advanced/runtime/docs/memory-management/#memory-management-for-executable-code
             * It seems to also be observed on .NET Framework to some extent, although no pattern is determined yet. Maybe x86 debug?
             *
             * In the future, this might end up requiring and calling new native platform methods.
             * Ideally this should be moved into the native platform which then uses some form of VirtualAlloc / mmap hackery.
             *
             * This is quite ugly, especially because we have no direct control over the allocated memory location nor size.
             * -ade
             */

            if (size == 0 ||
                size > _MemAllocScratchDummySafeSize) {
                ptr = IntPtr.Zero;
                return 0;
            }

            const long GB = 1024 * 1024 * 1024;
            bool isNonDynamic = Math.Abs((long) target - (long) ReferenceNonDynamicPoolPtr) < GB;
            bool isDynamic = DynamicMethodDefinition.IsDynamicILAvailable && Math.Abs((long) target - (long) ReferenceDynamicPoolPtr) < GB;
            if (!isNonDynamic && !isDynamic) {
                ptr = IntPtr.Zero;
                return 0;
            }

            MethodBase scratch;
            using (DynamicMethodDefinition copy = new DynamicMethodDefinition(_MemAllocScratchDummy)) {
                copy.Name = $"MemAllocScratch<{(long) target:X16}>";
                
                // On some versions of mono it is also possible to get dynamics close to non-dynamics by invoking before force-JITing.
                if (isDynamic)
                    scratch = DMDEmitDynamicMethodGenerator.Generate(copy);
                else
                    scratch = DMDCecilGenerator.Generate(copy);
            }

            Pin(scratch);
            ptr = GetNativeStart(scratch);
            DetourHelper.Native.MakeReadWriteExecutable(ptr, _MemAllocScratchDummySafeSize);
            return _MemAllocScratchDummySafeSize;
        }

        /* Random garbage method that should JIT into enough memory for us to write arbitrary data to,
         * but not too much for it to become wasteful once it's called often.
         * Use https://sharplab.io/ to estimate the footprint of the dummy when modifying it.
         * Make sure to measure both release and debug mode AND both x86 and x64 JIT results!
         * Neither mono nor ARM are options on sharplab.io, but hopefully it'll be enough as well...
         * Note that it MUST be public as there have been reported cases of visibility checks kicking in!
         * -ade
         */
        // Lowest measured so far: ret @ 0x19 on .NET Core x64 Release.
        protected static readonly uint _MemAllocScratchDummySafeSize = 16;
        protected static readonly MethodInfo _MemAllocScratchDummy =
            typeof(DetourRuntimeILPlatform).GetMethod(nameof(MemAllocScratchDummy), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        public static int MemAllocScratchDummy(int a, int b) {
            if (a >= 1024 && b >= 1024)
                return a + b;
            return MemAllocScratchDummy(a + b, b + 1);
        }

        protected class PrivateMethodPin {
            public MethodPinInfo Pin = new MethodPinInfo();
        }

        public struct MethodPinInfo {
            public int Count;
            public MethodBase Method;
            public RuntimeMethodHandle Handle;

            public override string ToString() {
                return $"(MethodPinInfo: {Count}, {Method}, 0x{(long) Handle.Value:X})";
            }
        }

        protected enum GlueThiscallStructRetPtrOrder {
            Original,
            ThisRetArgs,
            RetThisArgs
        }
    }
}
