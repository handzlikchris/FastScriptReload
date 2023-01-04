using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using Mono.Cecil.Cil;
using MCC = Mono.Cecil.Cil;
using SRE = System.Reflection.Emit;
using Mono.Cecil;
using OpCodes = Mono.Cecil.Cil.OpCodes;
using OpCode = Mono.Cecil.Cil.OpCode;
using Mono.Collections.Generic;
using ExceptionHandler = Mono.Cecil.Cil.ExceptionHandler;

namespace MonoMod.Utils.Cil {
    /// <summary>
    /// A variant of ILGenerator which uses Mono.Cecil under the hood.
    /// </summary>
#if !MONOMOD_INTERNAL
    public
#endif
    sealed class CecilILGenerator : ILGeneratorShim {
        // https://github.com/Unity-Technologies/mono/blob/unity-5.6/mcs/class/corlib/System.Reflection.Emit/LocalBuilder.cs
        // https://github.com/Unity-Technologies/mono/blob/unity-2018.3-mbe/mcs/class/corlib/System.Reflection.Emit/LocalBuilder.cs
        // https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/src/System/Reflection/Emit/LocalBuilder.cs
        // Mono: Type, ILGenerator
        // .NET Framework matches .NET Core: int, Type, MethodInfo(, bool)
        private static readonly ConstructorInfo c_LocalBuilder =
            typeof(LocalBuilder).GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length).First();
        private static readonly FieldInfo f_LocalBuilder_position =
            typeof(LocalBuilder).GetField("position", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo f_LocalBuilder_is_pinned =
            typeof(LocalBuilder).GetField("is_pinned", BindingFlags.NonPublic | BindingFlags.Instance);

        private static int c_LocalBuilder_params = c_LocalBuilder.GetParameters().Length;

        private static readonly Dictionary<short, OpCode> _MCCOpCodes = new Dictionary<short, OpCode>();

        private static Label NullLabel;

        static unsafe CecilILGenerator() {
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) {
                OpCode cecilOpCode = (OpCode) field.GetValue(null);
                _MCCOpCodes[cecilOpCode.Value] = cecilOpCode;
            }

            Label l = default;
            *(int*) &l = -1;
            NullLabel = l;
        }

        /// <summary>
        /// The underlying Mono.Cecil.Cil.ILProcessor.
        /// </summary>
        public readonly ILProcessor IL;

        private readonly Dictionary<Label, LabelInfo> _LabelInfos = new Dictionary<Label, LabelInfo>();
        private readonly List<LabelInfo> _LabelsToMark = new List<LabelInfo>();
        private readonly List<LabelledExceptionHandler> _ExceptionHandlersToMark = new List<LabelledExceptionHandler>();

        private readonly Dictionary<LocalBuilder, VariableDefinition> _Variables =
            new Dictionary<LocalBuilder, VariableDefinition>();

        private readonly Stack<ExceptionHandlerChain> _ExceptionHandlers = new Stack<ExceptionHandlerChain>();

        private int labelCounter;

        public CecilILGenerator(ILProcessor il) {
            IL = il;
        }

        private OpCode _(SRE.OpCode opcode) => _MCCOpCodes[opcode.Value];

        private LabelInfo _(Label handle) =>
            _LabelInfos.TryGetValue(handle, out LabelInfo labelInfo) ? labelInfo : null;

        private VariableDefinition _(LocalBuilder handle) => _Variables[handle];

        private TypeReference _(Type info) => IL.Body.Method.Module.ImportReference(info);
        private FieldReference _(FieldInfo info) => IL.Body.Method.Module.ImportReference(info);
        private MethodReference _(MethodBase info) => IL.Body.Method.Module.ImportReference(info);

        private int _ILOffset;
        public override int ILOffset => _ILOffset;

        private Instruction ProcessLabels(Instruction ins) {
            if (_LabelsToMark.Count != 0) {
                foreach (LabelInfo labelInfo in _LabelsToMark) {
                    foreach (Instruction insToFix in labelInfo.Branches) {
                        switch (insToFix.Operand) {
                            case Instruction insOperand:
                                insToFix.Operand = ins;
                                break;
                            case Instruction[] instrsOperand:
                                for (int i = 0; i < instrsOperand.Length; i++) {
                                    if (instrsOperand[i] == labelInfo.Instruction) {
                                        instrsOperand[i] = ins;
                                        break;
                                    }
                                }
                                break;
                        }
                    }

                    labelInfo.Emitted = true;
                    labelInfo.Instruction = ins;
                }
                
                _LabelsToMark.Clear();
            }

            if (_ExceptionHandlersToMark.Count != 0) {
                foreach (LabelledExceptionHandler exHandler in _ExceptionHandlersToMark)
                    IL.Body.ExceptionHandlers.Add(new ExceptionHandler(exHandler.HandlerType) {
                        TryStart = _(exHandler.TryStart)?.Instruction,
                        TryEnd = _(exHandler.TryEnd)?.Instruction,
                        HandlerStart = _(exHandler.HandlerStart)?.Instruction,
                        HandlerEnd = _(exHandler.HandlerEnd)?.Instruction,
                        FilterStart = _(exHandler.FilterStart)?.Instruction,
                        CatchType = exHandler.ExceptionType
                    });

                _ExceptionHandlersToMark.Clear();
            }

            return ins;
        }

        public override unsafe Label DefineLabel() {
            Label handle = default;
            // The label struct holds a single int field on .NET Framework, .NET Core and Mono.
            *(int*) &handle = labelCounter++;
            _LabelInfos[handle] = new LabelInfo();
            return handle;
        }

        public override void MarkLabel(Label loc) {
            if (!_LabelInfos.TryGetValue(loc, out LabelInfo labelInfo) || labelInfo.Emitted)
                return;
            _LabelsToMark.Add(labelInfo);
        }

        public override LocalBuilder DeclareLocal(Type type) => DeclareLocal(type, false);

        public override LocalBuilder DeclareLocal(Type type, bool pinned) {
            // The handle itself is out of sync with the "backing" VariableDefinition.
            int index = IL.Body.Variables.Count;
            LocalBuilder handle = (LocalBuilder) (
                c_LocalBuilder_params == 4 ? c_LocalBuilder.Invoke(new object[] { index, type, null, pinned }) :
                c_LocalBuilder_params == 3 ? c_LocalBuilder.Invoke(new object[] { index, type, null }) :
                c_LocalBuilder_params == 2 ? c_LocalBuilder.Invoke(new object[] { type, null }) :
                c_LocalBuilder_params == 0 ? c_LocalBuilder.Invoke(new object[] { }) :
                throw new NotSupportedException()
            );

            f_LocalBuilder_position?.SetValue(handle, (ushort) index);
            f_LocalBuilder_is_pinned?.SetValue(handle, pinned);

            TypeReference typeRef = _(type);
            if (pinned)
                typeRef = new PinnedType(typeRef);
            VariableDefinition def = new VariableDefinition(typeRef);
            IL.Body.Variables.Add(def);
            _Variables[handle] = def;

            return handle;
        }

        private void Emit(Instruction ins) {
            ins.Offset = _ILOffset;
            _ILOffset += ins.GetSize();
            IL.Append(ProcessLabels(ins));
        }

        public override void Emit(SRE.OpCode opcode) => Emit(IL.Create(_(opcode)));

        public override void Emit(SRE.OpCode opcode, byte arg) {
            if (opcode.OperandType == SRE.OperandType.ShortInlineVar ||
                opcode.OperandType == SRE.OperandType.InlineVar)
                _EmitInlineVar(_(opcode), arg);
            else
                Emit(IL.Create(_(opcode), arg));
        }

        public override void Emit(SRE.OpCode opcode, sbyte arg) {
            if (opcode.OperandType == SRE.OperandType.ShortInlineVar ||
                opcode.OperandType == SRE.OperandType.InlineVar)
                _EmitInlineVar(_(opcode), arg);
            else
                Emit(IL.Create(_(opcode), arg));
        }

        public override void Emit(SRE.OpCode opcode, short arg) {
            if (opcode.OperandType == SRE.OperandType.ShortInlineVar ||
                opcode.OperandType == SRE.OperandType.InlineVar)
                _EmitInlineVar(_(opcode), arg);
            else
                Emit(IL.Create(_(opcode), arg));
        }

        public override void Emit(SRE.OpCode opcode, int arg) {
            if (opcode.OperandType == SRE.OperandType.ShortInlineVar ||
                opcode.OperandType == SRE.OperandType.InlineVar)
                _EmitInlineVar(_(opcode), arg);
            else if (opcode.Name.EndsWith(".s", StringComparison.Ordinal))
                Emit(IL.Create(_(opcode), (sbyte) arg));
            else
                Emit(IL.Create(_(opcode), arg));
        }

        public override void Emit(SRE.OpCode opcode, long arg) => Emit(IL.Create(_(opcode), arg));
        public override void Emit(SRE.OpCode opcode, float arg) => Emit(IL.Create(_(opcode), arg));
        public override void Emit(SRE.OpCode opcode, double arg) => Emit(IL.Create(_(opcode), arg));
        public override void Emit(SRE.OpCode opcode, string arg) => Emit(IL.Create(_(opcode), arg));
        public override void Emit(SRE.OpCode opcode, Type arg) => Emit(IL.Create(_(opcode), _(arg)));
        public override void Emit(SRE.OpCode opcode, FieldInfo arg) => Emit(IL.Create(_(opcode), _(arg)));
        public override void Emit(SRE.OpCode opcode, ConstructorInfo arg) => Emit(IL.Create(_(opcode), _(arg)));
        public override void Emit(SRE.OpCode opcode, MethodInfo arg) => Emit(IL.Create(_(opcode), _(arg)));

        public override void Emit(SRE.OpCode opcode, Label label) {
            LabelInfo info = _(label);
            Instruction ins = IL.Create(_(opcode), _(label).Instruction);
            info.Branches.Add(ins);
            Emit(ProcessLabels(ins));
        }

        public override void Emit(SRE.OpCode opcode, Label[] labels) {
            IEnumerable<LabelInfo> labelInfos = labels.Distinct().Select(_);
            Instruction ins = IL.Create(_(opcode), labelInfos.Select(labelInfo => labelInfo.Instruction).ToArray());
            foreach (LabelInfo labelInfo in labelInfos)
                labelInfo.Branches.Add(ins);
            Emit(ProcessLabels(ins));
        }

        public override void Emit(SRE.OpCode opcode, LocalBuilder local) => Emit(IL.Create(_(opcode), _(local)));
        public override void Emit(SRE.OpCode opcode, SignatureHelper signature) => Emit(IL.Create(_(opcode), IL.Body.Method.Module.ImportCallSite(signature)));
        public void Emit(SRE.OpCode opcode, ICallSiteGenerator signature) => Emit(IL.Create(_(opcode), IL.Body.Method.Module.ImportCallSite(signature)));

        private void _EmitInlineVar(OpCode opcode, int index) {
            // System.Reflection.Emit has only got (Short)InlineVar and allows index refs.
            // Mono.Cecil has also got (Short)InlineArg and requires definition refs.
            switch (opcode.OperandType) {
                case MCC.OperandType.ShortInlineArg:
                case MCC.OperandType.InlineArg:
                    Emit(IL.Create(opcode, IL.Body.Method.Parameters[index]));
                    break;

                case MCC.OperandType.ShortInlineVar:
                case MCC.OperandType.InlineVar:
                    Emit(IL.Create(opcode, IL.Body.Variables[index]));
                    break;

                default:
                    throw new NotSupportedException(
                        $"Unsupported SRE InlineVar -> Cecil {opcode.OperandType} for {opcode} {index}");
            }
        }

        public override void EmitCall(SRE.OpCode opcode, MethodInfo methodInfo, Type[] optionalParameterTypes) =>
            Emit(IL.Create(_(opcode), _(methodInfo)));

        public override void EmitCalli(SRE.OpCode opcode, CallingConventions callingConvention, Type returnType,
            Type[] parameterTypes, Type[] optionalParameterTypes) => throw new NotSupportedException();

        public override void EmitCalli(SRE.OpCode opcode, CallingConvention unmanagedCallConv, Type returnType,
            Type[] parameterTypes) => throw new NotSupportedException();

        public override void EmitWriteLine(FieldInfo field) {
            if (field.IsStatic)
                Emit(IL.Create(OpCodes.Ldsfld, _(field)));
            else {
                Emit(IL.Create(OpCodes.Ldarg_0));
                Emit(IL.Create(OpCodes.Ldfld, _(field)));
            }

            Emit(IL.Create(OpCodes.Call, _(typeof(Console).GetMethod("WriteLine", new Type[1] {field.FieldType}))));
        }

        public override void EmitWriteLine(LocalBuilder localBuilder) {
            Emit(IL.Create(OpCodes.Ldloc, _(localBuilder)));
            Emit(IL.Create(OpCodes.Call,
                _(typeof(Console).GetMethod("WriteLine", new Type[1] {localBuilder.LocalType}))));
        }

        public override void EmitWriteLine(string value) {
            Emit(IL.Create(OpCodes.Ldstr, value));
            Emit(IL.Create(OpCodes.Call, _(typeof(Console).GetMethod("WriteLine", new Type[1] {typeof(string)}))));
        }

        public override void ThrowException(Type type) {
            Emit(IL.Create(OpCodes.Newobj, _(type.GetConstructor(Type.EmptyTypes))));
            Emit(IL.Create(OpCodes.Throw));
        }

        public override Label BeginExceptionBlock() {
            ExceptionHandlerChain chain = new ExceptionHandlerChain(this);
            _ExceptionHandlers.Push(chain);
            return chain.SkipAll;
        }

        public override void BeginCatchBlock(Type exceptionType) {
            LabelledExceptionHandler handler = _ExceptionHandlers.Peek().BeginHandler(ExceptionHandlerType.Catch);
            handler.ExceptionType = exceptionType == null ? null : _(exceptionType);
        }

        public override void BeginExceptFilterBlock() {
            _ExceptionHandlers.Peek().BeginHandler(ExceptionHandlerType.Filter);
        }

        public override void BeginFaultBlock() {
            _ExceptionHandlers.Peek().BeginHandler(ExceptionHandlerType.Fault);
        }

        public override void BeginFinallyBlock() {
            _ExceptionHandlers.Peek().BeginHandler(ExceptionHandlerType.Finally);
        }

        public override void EndExceptionBlock() {
            _ExceptionHandlers.Pop().End();
        }

        public override void BeginScope() {
        }

        public override void EndScope() {
        }

        public override void UsingNamespace(string usingNamespace) {
        }

        private class LabelInfo {
            public bool Emitted;
            public Instruction Instruction = Instruction.Create(OpCodes.Nop);
            public readonly List<Instruction> Branches = new List<Instruction>();
        }

        private class LabelledExceptionHandler {
            public Label TryStart = NullLabel;
            public Label TryEnd = NullLabel;
            public Label HandlerStart = NullLabel;
            public Label HandlerEnd = NullLabel;
            public Label FilterStart = NullLabel;
            public ExceptionHandlerType HandlerType;
            public TypeReference ExceptionType;
        }

        private class ExceptionHandlerChain {
            private readonly CecilILGenerator IL;

            private readonly Label _Start;
            public readonly Label SkipAll;
            private Label _SkipHandler;

            private LabelledExceptionHandler _Prev;
            private LabelledExceptionHandler _Handler;

            public ExceptionHandlerChain(CecilILGenerator il) {
                IL = il;

                _Start = il.DefineLabel();
                il.MarkLabel(_Start);

                SkipAll = il.DefineLabel();
            }

            public LabelledExceptionHandler BeginHandler(ExceptionHandlerType type) {
                LabelledExceptionHandler prev = _Prev = _Handler;
                if (prev != null)
                    EndHandler(prev);

                IL.Emit(SRE.OpCodes.Leave, _SkipHandler = IL.DefineLabel());

                Label handlerStart = IL.DefineLabel();
                IL.MarkLabel(handlerStart);

                LabelledExceptionHandler next = _Handler = new LabelledExceptionHandler {
                    TryStart = _Start, 
                    TryEnd = handlerStart, 
                    HandlerType = type, 
                    HandlerEnd = _SkipHandler
                };
                if (type == ExceptionHandlerType.Filter)
                    next.FilterStart = handlerStart;
                else
                    next.HandlerStart = handlerStart;

                return next;
            }

            public void EndHandler(LabelledExceptionHandler handler) {
                Label skip = _SkipHandler;

                switch (handler.HandlerType) {
                    case ExceptionHandlerType.Filter:
                        IL.Emit(SRE.OpCodes.Endfilter);
                        break;

                    case ExceptionHandlerType.Finally:
                        IL.Emit(SRE.OpCodes.Endfinally);
                        break;

                    default:
                        IL.Emit(SRE.OpCodes.Leave, skip);
                        break;
                }

                IL.MarkLabel(skip);
                IL._ExceptionHandlersToMark.Add(handler);
            }

            public void End() {
                EndHandler(_Handler);
                IL.MarkLabel(SkipAll);
            }
        }
    }
}