// These should be defined as part of your build process,
// but if you want to test them quickly...
// #define MONOMOD_RUNTIMEDETOUR_NET_SCAN_MANUAL
// #define MONOMOD_RUNTIMEDETOUR_NET_SCAN_AUTO
// Default to automatic only.
#if !MONOMOD_RUNTIMEDETOUR_NET_SCAN_MANUAL && !MONOMOD_RUNTIMEDETOUR_NET_SCAN_AUTO
#define MONOMOD_RUNTIMEDETOUR_NET_SCAN_AUTO
#endif

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;
using MonoMod.Utils;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace MonoMod.RuntimeDetour.Platforms {
#if !MONOMOD_INTERNAL
    public
#endif
    class DetourRuntimeNETPlatform : DetourRuntimeILPlatform {
        private static readonly object[] _NoArgs = new object[0];

        private static readonly Type _RTDynamicMethod =
            typeof(DynamicMethod).GetNestedType("RTDynamicMethod", BindingFlags.NonPublic);
        private static readonly FieldInfo _RTDynamicMethod_m_owner =
            _RTDynamicMethod?.GetField("m_owner", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo _DynamicMethod_m_method =
            typeof(DynamicMethod).GetField("m_method", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly MethodInfo _DynamicMethod_GetMethodDescriptor =
            typeof(DynamicMethod).GetMethod("GetMethodDescriptor", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _RuntimeMethodHandle_m_value =
            typeof(RuntimeMethodHandle).GetField("m_value", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly MethodInfo _IRuntimeMethodInfo_get_Value =
            typeof(RuntimeMethodHandle).Assembly.GetType("System.IRuntimeMethodInfo")?.GetMethod("get_Value");

        private static readonly MethodInfo _RuntimeHelpers__CompileMethod =
            typeof(RuntimeHelpers).GetMethod("_CompileMethod", BindingFlags.NonPublic | BindingFlags.Static) ??
            // System.Private.CoreLib 6.0.0.0
            typeof(RuntimeHelpers).GetMethod("CompileMethod", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly bool _RuntimeHelpers__CompileMethod_TakesIntPtr =
            _RuntimeHelpers__CompileMethod?.GetParameters()[0].ParameterType.FullName == "System.IntPtr";
        private static readonly bool _RuntimeHelpers__CompileMethod_TakesIRuntimeMethodInfo =
            _RuntimeHelpers__CompileMethod?.GetParameters()[0].ParameterType.FullName == "System.IRuntimeMethodInfo";
        private static readonly bool _RuntimeHelpers__CompileMethod_TakesRuntimeMethodHandleInternal =
            _RuntimeHelpers__CompileMethod?.GetParameters()[0].ParameterType.FullName == "System.RuntimeMethodHandleInternal";

        public override MethodBase GetIdentifiable(MethodBase method) {
            if (_RTDynamicMethod_m_owner != null && method.GetType() == _RTDynamicMethod)
                return (MethodBase) _RTDynamicMethod_m_owner.GetValue(method);
            return base.GetIdentifiable(method);
        }

        protected override RuntimeMethodHandle GetMethodHandle(MethodBase method) {
            // Compile the method handle before getting our hands on the final method handle.
            if (method is DynamicMethod dm) {
                if (_RuntimeHelpers__CompileMethod_TakesIntPtr) {
                    // mscorlib 2.0.0.0
                    _RuntimeHelpers__CompileMethod.Invoke(null, new object[] { ((RuntimeMethodHandle) _DynamicMethod_GetMethodDescriptor.Invoke(dm, _NoArgs)).Value });

                } else if (_RuntimeHelpers__CompileMethod_TakesIRuntimeMethodInfo) {
                    // mscorlib 4.0.0.0
                    _RuntimeHelpers__CompileMethod.Invoke(null, new object[] { _RuntimeMethodHandle_m_value.GetValue(((RuntimeMethodHandle) _DynamicMethod_GetMethodDescriptor.Invoke(dm, _NoArgs))) });

                } else if (_RuntimeHelpers__CompileMethod_TakesRuntimeMethodHandleInternal) {
                    // System.Private.CoreLib 5.0.0.0
                    _RuntimeHelpers__CompileMethod.Invoke(null, new object[] { _IRuntimeMethodInfo_get_Value.Invoke(_RuntimeMethodHandle_m_value.GetValue(((RuntimeMethodHandle) _DynamicMethod_GetMethodDescriptor.Invoke(dm, _NoArgs))), null) });

                } else {
                    // This should work just fine.
                    // It abuses the fact that CreateDelegate first compiles the DynamicMethod, before creating the delegate and failing.
                    // Only side effect: It introduces a possible deadlock in f.e. tModLoader, which adds a FirstChanceException handler.
                    try {
                        dm.CreateDelegate(typeof(MulticastDelegate));
                    } catch {
                    }
                }

                if (_DynamicMethod_m_method != null)
                    return (RuntimeMethodHandle) _DynamicMethod_m_method.GetValue(method);
                if (_DynamicMethod_GetMethodDescriptor != null)
                    return (RuntimeMethodHandle) _DynamicMethod_GetMethodDescriptor.Invoke(method, _NoArgs);
            }

            return method.MethodHandle;
        }

        protected override void DisableInlining(MethodBase method, RuntimeMethodHandle handle) {
            // This is not needed for .NET Framework - see DisableInliningTest.
        }

        protected override unsafe IntPtr GetFunctionPointer(MethodBase method, RuntimeMethodHandle handle) {
            MMDbgLog.Log($"mets: {method.GetID()}");
            MMDbgLog.Log($"meth: 0x{(long) handle.Value:X16}");
            MMDbgLog.Log($"getf: 0x{(long) handle.GetFunctionPointer():X16}");

            bool regenerated = false;
            bool wasPreStub;

            ReloadFuncPtr:

            IntPtr ptr;

            if (method.IsVirtual && (method.DeclaringType?.IsValueType ?? false)) {
                /* .NET has got TWO MethodDescs and thus TWO ENTRY POINTS for virtual struct methods (f.e. override ToString).
                 * More info: https://mattwarren.org/2017/08/02/A-look-at-the-internals-of-boxing-in-the-CLR/#unboxing-stub-creation
                 *
                 * Observations made so far:
                 * - GetFunctionPointer ALWAYS returns a pointer to the unboxing stub handle.
                 * - On x86, the "real" entry point is often found 8 bytes after the unboxing stub entry point.
                 * - Methods WILL be called INDIRECTLY using the pointer found in the "real" MethodDesc.
                 * - The "real" MethodDesc will be updated, which isn't an issue except that we can't patch the stub in time.
                 * - The "real" stub will stay untouched.
                 * - LDFTN RETURNS A POINTER TO THE "REAL" ENTRY POINT.
                 *
                 * Exceptions so far:
                 * - SOME interface methods seem to follow similar rules, but ldftn isn't enough.
                 * - Can't use GetBaseDefinition to check for interface methods as that holds up ALC unloading. (Mapping info is fine though...)
                 */
                MMDbgLog.Log($"ldfn: 0x{(long) method.GetLdftnPointer():X16}");
                bool interfaced = false;
                foreach (Type intf in method.DeclaringType.GetInterfaces()) {
                    if (method.DeclaringType.GetInterfaceMap(intf).TargetMethods.Contains(method)) {
                        interfaced = true;
                        break;
                    }
                }
                ptr = method.GetLdftnPointer();
                if (!interfaced) {
                    return ptr;
                }

            } else {
                // Your typical method.
                ptr = base.GetFunctionPointer(method, handle);
            }

            /* Many (if not all) NGEN'd methods (f.e. those from mscorlib.ni.dll) are handled in a special manner.
             * When debugged using WinDbg, !dumpmd for the handle gives a different CodeAddr than ldftn or GetFunctionPointer.
             * When using !ip2md on the ldftn / GetFunctionPointer result, no MD is found.
             * There is only one MD, we're already accessing it, but we still can't access the "real" entry point.
             * Luckily a jmp to it exists within the stub returned by GetFunctionPointer.
             * Sadly detecting when to read it is... ugly, to say the least.
             * This pretty much acts as the reverse of DetourNative*Platform.Apply
             * Maybe this should be Native*Platform-ified in the future, but for now...
             */

            // IMPORTANT: IN SOME CIRCUMSTANCES, THIS CAN FIND ThePreStub AS THE ENTRY POINT.

            if (PlatformHelper.Is(Platform.ARM)) {
                if (IntPtr.Size == 4) {
                    // TODO: 32-bit arm precode walkers
                } else {
                    IntPtr WalkPrecode(IntPtr curr) {
                        long lptr = (long) curr;

                        if (
                            // StubPrecode
                            // https://github.com/dotnet/runtime/blob/7830fddeead7907f6dd45f814fc3b8d49cd4b082/src/coreclr/vm/arm64/cgencpu.h#L567-L572
                            *(uint*) (lptr + 0x00) == 0x10000089 && // adr x9, #0x10
                            *(uint*) (lptr + 0x04) == 0xa940312a && // ldp x10, x12, [x9]
                            *(uint*) (lptr + 0x08) == 0xd61f0140    // br x10
                        ) {
                            IntPtr next = *(IntPtr*) (lptr + 0x10);
                            return NotThePreStub(curr, next, out wasPreStub);
                        } else if (
                            // NDirectImportPrecode
                            // https://github.com/dotnet/runtime/blob/7830fddeead7907f6dd45f814fc3b8d49cd4b082/src/coreclr/vm/arm64/cgencpu.h#L628-L633
                            *(uint*) (lptr + 0x00) == 0x1000008b && // adr x11, #0x10
                            *(uint*) (lptr + 0x04) == 0xa940316a && // ldp x10, x12, [x11]
                            *(uint*) (lptr + 0x08) == 0xd61f0140    // br x10
                        ) {
                            IntPtr next = *(IntPtr*) (lptr + 0x10);
                            return NotThePreStub(curr, next, out wasPreStub);
                        } else if (
                            // FixupPrecode
                            // https://github.com/dotnet/runtime/blob/7830fddeead7907f6dd45f814fc3b8d49cd4b082/src/coreclr/vm/arm64/cgencpu.h#L666-L672
                            *(uint*) (lptr + 0x00) == 0x1000000c && // adr x12, #0x00
                            *(uint*) (lptr + 0x04) == 0x5800006b && // ldr x11, #0x0c
                            *(uint*) (lptr + 0x08) == 0xd61f0160    // br x11
                        ) {
                            IntPtr next = *(IntPtr*) (lptr + 0x10);
                            return NotThePreStub(curr, next, out wasPreStub);
                        } else if (
                            // ThisPtrRetBufPrecode
                            // https://github.com/dotnet/runtime/blob/4da6b9a8d55913c0ea560d63590d35dc942425be/src/coreclr/vm/arm64/stubs.cpp#L641-L647
                            *(uint*) (lptr + 0x00) == 0x91000010 && // mov x16, x0
                            *(uint*) (lptr + 0x04) == 0x91000020 && // mov x0, x1
                            *(uint*) (lptr + 0x08) == 0x91000201 && // mov x1, x16
                            *(uint*) (lptr + 0x0c) == 0x58000070 && // ldr x16, #0x0c
                            *(uint*) (lptr + 0x10) == 0xd61f0200    // br x16
                        ) {
                            IntPtr next = *(IntPtr*) (lptr + 0x18);
                            return NotThePreStub(curr, next, out wasPreStub);
                        }

                        return curr;
                    }

                    int numIterations = 0;

                    wasPreStub = false;
                    IntPtr nextPtr = WalkPrecode(ptr);
                    if (wasPreStub) {
                        PrepareMethod(method, handle);
                        goto ReloadFuncPtr;
                    }
                    while (nextPtr != ptr && numIterations < 16) {
                        numIterations++;
                        ptr = nextPtr;

                        wasPreStub = false;
                        nextPtr = WalkPrecode(ptr);
                        if (wasPreStub) {
                            PrepareMethod(method, handle);
                            goto ReloadFuncPtr;
                        }
                    }
                }
            } else if (IntPtr.Size == 4) {
                int iptr = (int) ptr;
                // x86
                if (*(byte*) (iptr + 0x00) == 0xb8 && // mov ... (mscorlib_ni!???)
                    *(byte*) (iptr + 0x05) == 0x90 && // nop
                    *(byte*) (iptr + 0x06) == 0xe8 && // call ... (clr!PrecodeRemotingThunk)
                    *(byte*) (iptr + 0x0b) == 0xe9 // jmp {DELTA}
                ) {
                    // delta = to - (from + 1 + sizeof(int))
                    // to = delta + (from + 1 + sizeof(int))
                    int from = iptr + 0x0b;
                    int delta = *(int*) (from + 1);
                    int to = delta + (from + 1 + sizeof(int));
                    ptr = NotThePreStub(ptr, (IntPtr) to, out wasPreStub);
                    if (wasPreStub) {
                        PrepareMethod(method, handle);
                        goto ReloadFuncPtr;
                    }
                    MMDbgLog.Log($"ngen: 0x{(long) ptr:X8}");
                }

                // .NET Core and .NET Framework 4.8..?
                iptr = (int) ptr;
                if (*(byte*) (iptr + 0x00) == 0xe9 && // jmp {DELTA}
                    *(byte*) (iptr + 0x05) == 0x5f // pop rdi
                ) {
                    // delta = to - (from + 1 + sizeof(int))
                    // to = delta + (from + 1 + sizeof(int))
                    int from = iptr;
                    int delta = *(int*) (from + 1);
                    int to = delta + (from + 1 + sizeof(int));
                    ptr = NotThePreStub(ptr, (IntPtr) to, out wasPreStub);
                    if (wasPreStub) {
                        PrepareMethod(method, handle);
                        goto ReloadFuncPtr;
                    }
                    MMDbgLog.Log($"ngen: 0x{(int) ptr:X8}");
                }

            } else {
                long lptr = (long) ptr;
                // x64 .NET Framework
                if (*(uint*) (lptr + 0x00) == 0x74___c9_85_48 && // in reverse order: test rcx, rcx | je ...
                    *(uint*) (lptr + 0x05) == 0x49___01_8b_48 && // in reverse order: rax, qword ptr [rcx] | mov ...
                    *(uint*) (lptr + 0x12) == 0x74___c2_3b_49 && // in reverse order: cmp rax, r10 | je ...
                    *(ushort*) (lptr + 0x17) == 0xb8_48 // in reverse order: mov {TARGET}
                ) {
                    ptr = NotThePreStub(ptr, (IntPtr) (*(ulong*) (lptr + 0x19)), out wasPreStub);
                    if (wasPreStub) {
                        PrepareMethod(method, handle);
                        goto ReloadFuncPtr;
                    }
                    MMDbgLog.Log($"ngen: 0x{(long) ptr:X16}");
                    return ptr;
                }

                // FIXME: on Core, it seems that *every* method has this stub, not just NGEN'd methods
                //        It also seems to correctly find the body, but because ThePreStub is always -1,
                //          it never returns that.
                //        One consequence of this seems to be that re-JITting a method calling a patched
                //          method causes it to use a new stub, except not patched.

                // It seems that if there is *any* pause between the method being prepared, and this being
                //   called, there is a chance that the JIT will do something funky and reset the thunk for
                //   the method (which is what GetFunctionPointer gives) back to a call to PrecodeFixupThunk.
                // This can be observed by checking for the first byte being 0xe8 instead of 0xe9.
                // If this happens at the wrong moment, we won't get the opportunity to patch the actual method
                //   body because our only pointer to it will have been deleted.
                
                // In conclusion: *Do we need to disable re-JITing while patching?*

                // Correction for the above: It seems that .NET Core ALWAYS has one indirection before the method
                //   body, and that indirection is used as an easy way to call into the JIT when necessary. Also,
                //   the JIT never generates a call directly to ThePreStub, but instead generates a call to
                //   PrecodeFixupThunk which then calls ThePreStub.

                // x64 .NET Core
                if (*(byte*) (lptr + 0x00) == 0xe9 &&   // jmp {DELTA}
                    *(byte*) (lptr + 0x05) == 0x5f      // pop rdi
                ) {
                    // delta = to - (from + 1 + sizeof(int))
                    // to = delta + (from + 1 + sizeof(int))
                    long from = lptr;
                    int delta = *(int*) (from + 1);
                    long to = delta + (from + 1 + sizeof(int));
                    ptr = NotThePreStub(ptr, (IntPtr) to, out wasPreStub);
                    if (wasPreStub) {
                        PrepareMethod(method, handle);
                        goto ReloadFuncPtr;
                    }
#if MONOMOD_RUNTIMEDETOUR_NET_SCAN_MANUAL
                    // This ain't enough though! Turns out if we stop here, ptr is in a region that can be free'd,
                    // while the *actual actual* method body can still remain in memory. What even is this limbo?
                    // Let's try to navigate out of here by using further guesswork.
                    lptr = (long) ptr;
                    if (*(ushort*) (lptr + 0x00)                == 0xb8_48 &&       // movabs rax, ???
                        ((*(uint*) (lptr + 0x0A)) & 0x00ffffff) == 0x__08_ff_66 &&  // dec WORD PTR [rax]
                        *(ushort*) (lptr + 0x0D)                == 0x85_0f          // jne {DELTA}
                    ) {
                        from = lptr;
                        delta = *(int*) (from + 0x0D + 2);
                        to = delta + (from + 0x0D + 2 + sizeof(int));
                        // Noticed this by sheer luck. Maybe a link to the coreclr source would be neat in the future tho.
                        if ((*(long*) ((long) handle.Value + 0x10)) == to ||
                            (*(long*) ((long) handle.Value + 0x18)) == to) {
                            ptr = NotThePreStub(ptr, (IntPtr) to, out wasPreStub);
                            if (wasPreStub) {
                                PrepareMethod(method, handle);
                                goto ReloadFuncPtr;
                            }
                        }
                    }
                    // Generics are pain.
                    if (*(byte*) (lptr + 0x01)      == 0xb8 &&      // movabs {last arg + 1}, {generic type handle}
                        *(ushort*) (lptr + 0x0A)    == 0xb8_48 &&   // movabs rax, {TARGET}
                        *(ushort*) (lptr + 0x14)    == 0xe0_ff      // jmp rax
                    ) {
                        from = lptr;
                        long typeHandle = *(long*) (from + 0x00 + 2);
                        to = *(long*) (from + 0x0A + 2);
                        // Yet another coincidence to add to the "find in coreclr src please" list.
                        if (method.DeclaringType.TypeHandle.Value == (IntPtr) typeHandle) {
                            ptr = NotThePreStub(ptr, (IntPtr) to, out wasPreStub);
                            if (wasPreStub) {
                                PrepareMethod(method, handle);
                                goto ReloadFuncPtr;
                            }
                        }
                    }
#endif
#if MONOMOD_RUNTIMEDETOUR_NET_SCAN_AUTO
                    // And because we don't know what else awaits us, let's auto-scan!
                    for (int scan = 0; scan < 16; scan++) {
                        lptr = (long) ptr + scan;
                        if (*(ushort*) (lptr + 0x00)    == 0xb8_48 &&   // movabs rax, {PTR}
                            *(ushort*) (lptr + 0x0A)    == 0xe0_ff      // jmp rax
                        ) {
                            to = *(long*) (lptr + 0x02);
                            ptr = NotThePreStub(ptr, (IntPtr) to, out wasPreStub);
                            if (wasPreStub) {
                                PrepareMethod(method, handle);
                                goto ReloadFuncPtr;
                            }
                            scan = -1;
                            continue;
                        }
                        if ((*(ushort*) (lptr + 0x00) & 0xfff0)     == 0xb8_40      &&  // movabs ??1, ???
                            ((*(uint*) (lptr + 0x0A)) & 0x00f0ffff) == 0x__00_ff_66 &&  // dec WORD PTR [??1]
                            *(ushort*) (lptr + 0x0D)                == 0x85_0f      &&  // jne {DELTA}
                            (*(byte*) (lptr + 0x00) & 0x0f) == (*(byte*) (lptr + 0x0C) & 0x0f) // ??1 == ??1
                        ) {
                            from = lptr;
                            delta = *(int*) (from + 0x0D + 2);
                            to = delta + (from + 0x0D + 2 + sizeof(int));
                            ptr = NotThePreStub(ptr, (IntPtr) to, out wasPreStub);
                            if (wasPreStub) {
                                PrepareMethod(method, handle);
                                goto ReloadFuncPtr;
                            }
                            scan = -1;
                            continue;
                        }
                    }
#endif
#if MONOMOD_RUNTIMEDETOUR_NET_SCAN_MANUAL && !MONOMOD_RUNTIMEDETOUR_NET_SCAN_AUTO
                    // And apparently if we're on wine, it likes to fool us really hard.
                    // HEY WINE DEVS: Feel free to poke me about this. I'm hating this too.
                    // HEY VALVE: If you're seeing this, thanks for lying about "everything works~"
                    // Check for this when *only* scanning manually, as auto-scanning finds this too.
                    lptr = (long) ptr;
                    if (PlatformHelper.Is(Platform.Wine) &&
                        *(ushort*) (lptr + 0x00) == 0xb8_48 && // movabs rax, {PTR}
                        *(ushort*) (lptr + 0x0A) == 0xe0_ff // jmp rax
                    ) {
                        to = *(long*) (lptr + 0x02);
                        ptr = NotThePreStub(ptr, (IntPtr) to, out wasPreStub);
                        if (wasPreStub) {
                            PrepareMethod(method, handle);
                            goto ReloadFuncPtr;
                        }
                    }
#endif
                    MMDbgLog.Log($"ngen: 0x{(long) ptr:X16}");
                    return ptr;
                }

                // x64 .NET Core, but the thunk was reset
                // This can also just be an optimized method immediately calling another method.
                if (*(byte*) (lptr + 0x00) == 0xe8 && !regenerated) { // call
                    MMDbgLog.Log($"Method thunk reset; regenerating");
                    regenerated = true;
                    int precodeThunkOffset = *(int*) (lptr + 1);
                    long precodeThunk = precodeThunkOffset + (lptr + 1 + sizeof(int));
                    MMDbgLog.Log($"PrecodeFixupThunk: 0x{precodeThunk:X16}");
                    PrepareMethod(method, handle);
                    goto ReloadFuncPtr;
                }
            }


            return ptr;
        }

        private static IntPtr ThePreStub = IntPtr.Zero;

        public override bool OnMethodCompiledWillBeCalled => false;
#pragma warning disable CS0067 // Event never fired
        public override event OnMethodCompiledEvent OnMethodCompiled;
#pragma warning restore CS0067

        private IntPtr NotThePreStub(IntPtr ptrGot, IntPtr ptrParsed, out bool wasPreStub) {
            if (ThePreStub == IntPtr.Zero) {
                ThePreStub = (IntPtr) (-2);

                // FIXME: Find a better less likely called NGEN'd candidate that points to ThePreStub.
                // This was "found" by tModLoader.
                // Can be missing in .NET 5.0 outside of Windows for some reason.
                MethodInfo mi = typeof(System.Net.HttpWebRequest).Assembly
                    .GetType("System.Net.Connection")
                    ?.GetMethod("SubmitRequest", BindingFlags.NonPublic | BindingFlags.Instance);

                if (mi != null) {
                    ThePreStub = GetNativeStart(mi);
                    MMDbgLog.Log($"ThePreStub: 0x{(long) ThePreStub:X16}");
                } else if (PlatformHelper.Is(Platform.Windows)) {
                    // FIXME: This should be -1 (always return ptrGot) on all plats, but SubmitRequest is Windows-only?
                    ThePreStub = (IntPtr) (-1);
                }
            }

            wasPreStub = ptrParsed == ThePreStub /*|| ThePreStub == (IntPtr) (-1)*/;

            return wasPreStub ? ptrGot : ptrParsed;
        }
    }
}
