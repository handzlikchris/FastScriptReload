using System;
using System.Reflection;
using SRE = System.Reflection.Emit;
using CIL = Mono.Cecil.Cil;
using System.Linq.Expressions;
using MonoMod.Utils;
using System.Collections.Generic;
using Mono.Cecil;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    static class DynamicMethodHelper {

        // Used in EmitReference.
        private static List<object> References = new List<object>();
        public static object GetReference(int id) => References[id];
        public static void SetReference(int id, object obj) => References[id] = obj;
        private static int AddReference(object obj) {
            lock (References) {
                References.Add(obj);
                return References.Count - 1;
            }
        }
        public static void FreeReference(int id) => References[id] = null;

        private static readonly MethodInfo _GetMethodFromHandle = typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) });
        private static readonly MethodInfo _GetReference = typeof(DynamicMethodHelper).GetMethod("GetReference");

        /// <summary>
        /// Fill the DynamicMethod with a stub.
        /// </summary>
        public static SRE.DynamicMethod Stub(this SRE.DynamicMethod dm) {
            SRE.ILGenerator il = dm.GetILGenerator();
            for (int i = 0; i < 32; i++) {
                // Prevent mono from inlining the DynamicMethod.
                il.Emit(SRE.OpCodes.Nop);
            }
            if (dm.ReturnType != typeof(void)) {
                il.DeclareLocal(dm.ReturnType);
                il.Emit(SRE.OpCodes.Ldloca_S, (sbyte) 0);
                il.Emit(SRE.OpCodes.Initobj, dm.ReturnType);
                il.Emit(SRE.OpCodes.Ldloc_0);
            }
            il.Emit(SRE.OpCodes.Ret);
            return dm;
        }

        /// <summary>
        /// Fill the DynamicMethod with a stub.
        /// </summary>
        public static DynamicMethodDefinition Stub(this DynamicMethodDefinition dmd) {
            CIL.ILProcessor il = dmd.GetILProcessor();
            for (int i = 0; i < 32; i++) {
                // Prevent mono from inlining the DynamicMethod.
                il.Emit(CIL.OpCodes.Nop);
            }
            if (dmd.Definition.ReturnType != dmd.Definition.Module.TypeSystem.Void) {
                il.Body.Variables.Add(new CIL.VariableDefinition(dmd.Definition.ReturnType));
                il.Emit(CIL.OpCodes.Ldloca_S, (sbyte) 0);
                il.Emit(CIL.OpCodes.Initobj, dmd.Definition.ReturnType);
                il.Emit(CIL.OpCodes.Ldloc_0);
            }
            il.Emit(CIL.OpCodes.Ret);
            return dmd;
        }

        /// <summary>
        /// Emit a reference to an arbitrary object. Note that the references "leak."
        /// </summary>
        public static int EmitReference<T>(this SRE.ILGenerator il, T obj) {
            Type t = typeof(T);
            int id = AddReference(obj);
            il.Emit(SRE.OpCodes.Ldc_I4, id);
            il.Emit(SRE.OpCodes.Call, _GetReference);
            if (t.IsValueType)
                il.Emit(SRE.OpCodes.Unbox_Any, t);
            return id;
        }

        /// <summary>
        /// Emit a reference to an arbitrary object. Note that the references "leak."
        /// </summary>
        public static int EmitReference<T>(this CIL.ILProcessor il, T obj) {
            ModuleDefinition ilModule = il.Body.Method.Module;
            Type t = typeof(T);
            int id = AddReference(obj);
            il.Emit(CIL.OpCodes.Ldc_I4, id);
            il.Emit(CIL.OpCodes.Call, ilModule.ImportReference(_GetReference));
            if (t.IsValueType)
                il.Emit(CIL.OpCodes.Unbox_Any, ilModule.ImportReference(t));
            return id;
        }

        /// <summary>
        /// Emit a reference to an arbitrary object. Note that the references "leak."
        /// </summary>
        public static int EmitGetReference<T>(this SRE.ILGenerator il, int id) {
            Type t = typeof(T);
            il.Emit(SRE.OpCodes.Ldc_I4, id);
            il.Emit(SRE.OpCodes.Call, _GetReference);
            if (t.IsValueType)
                il.Emit(SRE.OpCodes.Unbox_Any, t);
            return id;
        }

        /// <summary>
        /// Emit a reference to an arbitrary object. Note that the references "leak."
        /// </summary>
        public static int EmitGetReference<T>(this CIL.ILProcessor il, int id) {
            ModuleDefinition ilModule = il.Body.Method.Module;
            Type t = typeof(T);
            il.Emit(CIL.OpCodes.Ldc_I4, id);
            il.Emit(CIL.OpCodes.Call, ilModule.ImportReference(_GetReference));
            if (t.IsValueType)
                il.Emit(CIL.OpCodes.Unbox_Any, ilModule.ImportReference(t));
            return id;
        }

    }
}
