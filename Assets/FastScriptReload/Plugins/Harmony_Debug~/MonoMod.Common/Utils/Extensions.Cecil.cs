using System;
using SRE = System.Reflection.Emit;
using CIL = Mono.Cecil.Cil;
using System.Linq.Expressions;
using MonoMod.Utils;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using Mono.Cecil;
using System.Text;
using Mono.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    static partial class Extensions {

        /// <summary>
        /// Safely resolve a reference, silently discarding any exceptions.
        /// </summary>
        /// <param name="r">The reference to resolve.</param>
        /// <returns>The resolved definition or null.</returns>
        public static TypeDefinition SafeResolve(this TypeReference r) {
            try {
                return r.Resolve();
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Safely resolve a reference, silently discarding any exceptions.
        /// </summary>
        /// <param name="r">The reference to resolve.</param>
        /// <returns>The resolved definition or null.</returns>
        public static FieldDefinition SafeResolve(this FieldReference r) {
            try {
                return r.Resolve();
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Safely resolve a reference, silently discarding any exceptions.
        /// </summary>
        /// <param name="r">The reference to resolve.</param>
        /// <returns>The resolved definition or null.</returns>
        public static MethodDefinition SafeResolve(this MethodReference r) {
            try {
                return r.Resolve();
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Safely resolve a reference, silently discarding any exceptions.
        /// </summary>
        /// <param name="r">The reference to resolve.</param>
        /// <returns>The resolved definition or null.</returns>
        public static PropertyDefinition SafeResolve(this PropertyReference r) {
            try {
                return r.Resolve();
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Get a certain custom attribute from an attribute provider.
        /// </summary>
        /// <param name="cap">The attribute provider.</param>
        /// <param name="attribute">The custom attribute name.</param>
        /// <returns>The first matching custom attribute, or null if no matching attribute has been found.</returns>
        public static CustomAttribute GetCustomAttribute(this ICustomAttributeProvider cap, string attribute) {
            if (cap == null || !cap.HasCustomAttributes)
                return null;
            foreach (CustomAttribute attrib in cap.CustomAttributes)
                if (attrib.AttributeType.FullName == attribute)
                    return attrib;
            return null;
        }

        /// <summary>
        /// Determine if an attribute provider has got a specific custom attribute.
        /// </summary>
        /// <param name="cap">The attribute provider.</param>
        /// <param name="attribute">The custom attribute name.</param>
        /// <returns>true if the attribute provider contains the given custom attribute, false otherwise.</returns>
        public static bool HasCustomAttribute(this ICustomAttributeProvider cap, string attribute)
            => cap.GetCustomAttribute(attribute) != null;

        /// <summary>
        /// Get the integer value pushed onto the stack with this instruction.
        /// </summary>
        /// <param name="instr">The instruction to get the pushed integer value for.</param>
        /// <returns>The pushed integer value.</returns>
        public static int GetInt(this Instruction instr) {
            OpCode op = instr.OpCode;
            if (op == OpCodes.Ldc_I4_M1)
                return -1;
            if (op == OpCodes.Ldc_I4_0)
                return 0;
            if (op == OpCodes.Ldc_I4_1)
                return 1;
            if (op == OpCodes.Ldc_I4_2)
                return 2;
            if (op == OpCodes.Ldc_I4_3)
                return 3;
            if (op == OpCodes.Ldc_I4_4)
                return 4;
            if (op == OpCodes.Ldc_I4_5)
                return 5;
            if (op == OpCodes.Ldc_I4_6)
                return 6;
            if (op == OpCodes.Ldc_I4_7)
                return 7;
            if (op == OpCodes.Ldc_I4_8)
                return 8;
            if (op == OpCodes.Ldc_I4_S)
                return (sbyte) instr.Operand;
            return (int) instr.Operand;
        }
        /// <summary>
        /// Get the integer value pushed onto the stack with this instruction.
        /// </summary>
        /// <param name="instr">The instruction to get the pushed integer value for.</param>
        /// <returns>The pushed integer value or null.</returns>
        public static int? GetIntOrNull(this Instruction instr) {
            OpCode op = instr.OpCode;
            if (op == OpCodes.Ldc_I4_M1)
                return -1;
            if (op == OpCodes.Ldc_I4_0)
                return 0;
            if (op == OpCodes.Ldc_I4_1)
                return 1;
            if (op == OpCodes.Ldc_I4_2)
                return 2;
            if (op == OpCodes.Ldc_I4_3)
                return 3;
            if (op == OpCodes.Ldc_I4_4)
                return 4;
            if (op == OpCodes.Ldc_I4_5)
                return 5;
            if (op == OpCodes.Ldc_I4_6)
                return 6;
            if (op == OpCodes.Ldc_I4_7)
                return 7;
            if (op == OpCodes.Ldc_I4_8)
                return 8;
            if (op == OpCodes.Ldc_I4_S)
                return (sbyte) instr.Operand;
            if (op == OpCodes.Ldc_I4)
                return (int) instr.Operand;
            return null;
        }

        /// <summary>
        /// Determine if the method call is a base method call.
        /// </summary>
        /// <param name="body">The caller method body.</param>
        /// <param name="called">The called method.</param>
        /// <returns>True if the called method is a base method of the caller method, false otherwise.</returns>
        public static bool IsBaseMethodCall(this MethodBody body, MethodReference called) {
            MethodDefinition caller = body.Method;
            if (called == null)
                return false;
            TypeReference calledType = called.DeclaringType;
            while (calledType is TypeSpecification)
                calledType = ((TypeSpecification) calledType).ElementType;
            string calledTypeName = calledType.GetPatchFullName();

            bool callingBaseType = false;
            try {
                TypeDefinition baseType = caller.DeclaringType;
                while ((baseType = baseType.BaseType?.SafeResolve()) != null)
                    if (baseType.GetPatchFullName() == calledTypeName) {
                        callingBaseType = true;
                        break;
                    }
            } catch {
                callingBaseType = caller.DeclaringType.GetPatchFullName() == calledTypeName;
            }
            if (!callingBaseType)
                return false;

            // return caller.IsMatchingSignature(called);
            return true;
        }

        /// <summary>
        /// Determine if the given method can be preferably called using callvirt.
        /// </summary>
        /// <param name="method">The called method.</param>
        /// <returns>True if the called method can be called using callvirt, false otherwise.</returns>
        public static bool IsCallvirt(this MethodReference method) {
            if (!method.HasThis)
                return false;
            if (method.DeclaringType.IsValueType)
                return false;
            return true;
        }

        /// <summary>
        /// Determine if the given type is a struct (also known as "value type") or struct-alike (f.e. primitive).
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is a struct, primitive or similar, false otherwise.</returns>
        public static bool IsStruct(this TypeReference type) {
            if (!type.IsValueType)
                return false;
            if (type.IsPrimitive)
                return false;
            return true;
        }

    }
}
