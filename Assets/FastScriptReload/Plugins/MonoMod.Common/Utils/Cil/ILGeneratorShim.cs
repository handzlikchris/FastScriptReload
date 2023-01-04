using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MonoMod.Utils.Cil {
    /// <summary>
    /// Abstract version of System.Reflection.Emit.ILGenerator. See <see cref="System.Reflection.Emit.ILGenerator"/> for proper documentation.
    /// </summary>
#if !MONOMOD_INTERNAL
    public
#endif
    abstract partial class ILGeneratorShim {

        public abstract int ILOffset { get; }
        public abstract void BeginCatchBlock(Type exceptionType);
        public abstract void BeginExceptFilterBlock();
        public abstract System.Reflection.Emit.Label BeginExceptionBlock();
        public abstract void BeginFaultBlock();
        public abstract void BeginFinallyBlock();
        public abstract void BeginScope();
        public abstract System.Reflection.Emit.LocalBuilder DeclareLocal(Type localType);
        public abstract System.Reflection.Emit.LocalBuilder DeclareLocal(Type localType, bool pinned);
        public abstract System.Reflection.Emit.Label DefineLabel();
        public abstract void Emit(System.Reflection.Emit.OpCode opcode);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, byte arg);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, double arg);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, short arg);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, int arg);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, long arg);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, ConstructorInfo con);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, System.Reflection.Emit.Label label);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, System.Reflection.Emit.Label[] labels);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, System.Reflection.Emit.LocalBuilder local);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, System.Reflection.Emit.SignatureHelper signature);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, FieldInfo field);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, MethodInfo meth);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, sbyte arg);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, float arg);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, string str);
        public abstract void Emit(System.Reflection.Emit.OpCode opcode, Type cls);
        public abstract void EmitCall(System.Reflection.Emit.OpCode opcode, MethodInfo methodInfo, Type[] optionalParameterTypes);
        public abstract void EmitCalli(System.Reflection.Emit.OpCode opcode, CallingConventions callingConvention, Type returnType, Type[] parameterTypes, Type[] optionalParameterTypes);
        public abstract void EmitCalli(System.Reflection.Emit.OpCode opcode, System.Runtime.InteropServices.CallingConvention unmanagedCallConv, Type returnType, Type[] parameterTypes);
        public abstract void EmitWriteLine(System.Reflection.Emit.LocalBuilder localBuilder);
        public abstract void EmitWriteLine(FieldInfo fld);
        public abstract void EmitWriteLine(string value);
        public abstract void EndExceptionBlock();
        public abstract void EndScope();
        public abstract void MarkLabel(System.Reflection.Emit.Label loc);
        public abstract void ThrowException(Type excType);
        public abstract void UsingNamespace(string usingNamespace);

    }
}
