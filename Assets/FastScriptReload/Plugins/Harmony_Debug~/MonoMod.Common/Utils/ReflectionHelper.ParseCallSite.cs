using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq.Expressions;
using MonoMod.Utils;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    // If you've thought DMDEmit.EmitCallSite.cs was bad enough, look away.
    static partial class ReflectionHelper {

        // https://github.com/dotnet/runtime/blob/10717887317beb824e57cdb29417663615211e99/src/coreclr/src/System.Private.CoreLib/src/System/Reflection/Emit/SignatureHelper.cs#L191
        // https://github.com/mono/mono/blob/1317cf06da06682419f8f4b0c9810ad5d5d3ac3a/mcs/class/corlib/System.Reflection.Emit/SignatureHelper.cs#L55
        private static readonly FieldInfo f_SignatureHelper_module =
            typeof(SignatureHelper).GetField("m_module", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance) ??
            typeof(SignatureHelper).GetField("module", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        public static CallSite ImportCallSite(this ModuleDefinition moduleTo, ICallSiteGenerator signature)
            => signature.ToCallSite(moduleTo);
        public static CallSite ImportCallSite(this ModuleDefinition moduleTo, SignatureHelper signature)
            => moduleTo.ImportCallSite(f_SignatureHelper_module.GetValue(signature) as Module, signature.GetSignature());
        public static CallSite ImportCallSite(this ModuleDefinition moduleTo, Module moduleFrom, int token)
            => moduleTo.ImportCallSite(moduleFrom, moduleFrom.ResolveSignature(token));
        public static CallSite ImportCallSite(this ModuleDefinition moduleTo, Module moduleFrom, byte[] data) {
            CallSite callsite = new CallSite(moduleTo.TypeSystem.Void);

            // Based on https://github.com/jbevain/cecil/blob/96026325ee1cb6627a3e4a32b924ab2905f02553/Mono.Cecil/AssemblyReader.cs#L3448

            using (MemoryStream stream = new MemoryStream(data, false))
            using (BinaryReader reader = new BinaryReader(stream)) {
                ReadMethodSignature(callsite);
                return callsite;

                void ReadMethodSignature(IMethodSignature method) {
                    byte callConv = reader.ReadByte();

                    if ((callConv & 0x20) != 0) {
                        method.HasThis = true;
                        callConv = (byte) (callConv & ~0x20);
                    }

                    if ((callConv & 0x40) != 0) {
                        method.ExplicitThis = true;
                        callConv = (byte) (callConv & ~0x40);
                    }

                    method.CallingConvention = (MethodCallingConvention) callConv;

                    if ((callConv & 0x10) != 0) {
                        uint arity = ReadCompressedUInt32();
                        // Shouldn't apply to CallSites.
                    }

                    uint paramCount = ReadCompressedUInt32();

                    method.MethodReturnType.ReturnType = ReadTypeSignature();

                    for (int i = 0; i < paramCount; i++)
                        method.Parameters.Add(new ParameterDefinition(ReadTypeSignature()));
                }

                uint ReadCompressedUInt32() {
                    byte first = reader.ReadByte();
                    if ((first & 0x80) == 0)
                        return first;

                    if ((first & 0x40) == 0)
                        return ((uint) (first & ~0x80) << 8)
                            | reader.ReadByte();

                    return ((uint) (first & ~0xc0) << 24)
                        | (uint) reader.ReadByte() << 16
                        | (uint) reader.ReadByte() << 8
                        | reader.ReadByte();
                }

                int ReadCompressedInt32() {
                    byte b = reader.ReadByte();
                    reader.BaseStream.Seek(-1, SeekOrigin.Current);
                    int u = (int) ReadCompressedUInt32();
                    int v = u >> 1;
                    if ((u & 1) == 0)
                        return v;

                    switch (b & 0xc0) {
                        case 0:
                        case 0x40:
                            return v - 0x40;

                        case 0x80:
                            return v - 0x2000;

                        default:
                            return v - 0x10000000;
                    }
                }

                TypeReference GetTypeDefOrRef() {
                    uint tokenData = ReadCompressedUInt32();

                    uint rid = tokenData >> 2;
                    uint token;
                    switch (tokenData & 3) {
                        case 0:
                            token = (uint) TokenType.TypeDef | rid;
                            break;

                        case 1:
                            token = (uint) TokenType.TypeRef | rid;
                            break;

                        case 2:
                            token = (uint) TokenType.TypeSpec | rid;
                            break;

                        default:
                            token = 0;
                            break;
                    }

                    return moduleTo.ImportReference(moduleFrom.ResolveType((int) token));
                }

                TypeReference ReadTypeSignature() {
                    MetadataType etype = (MetadataType) reader.ReadByte();
                    switch (etype) {
                        case MetadataType.ValueType:
                        case MetadataType.Class:
                            return GetTypeDefOrRef();

                        case MetadataType.Pointer:
                            return new PointerType(ReadTypeSignature());

                        case MetadataType.FunctionPointer:
                            FunctionPointerType fptr = new FunctionPointerType();
                            ReadMethodSignature(fptr);
                            return fptr;

                        case MetadataType.ByReference:
                            return new ByReferenceType(ReadTypeSignature());

                        case MetadataType.Pinned:
                            return new PinnedType(ReadTypeSignature());

                        case (MetadataType) 0x1d: // SzArray
                            return new ArrayType(ReadTypeSignature());

                        case MetadataType.Array:
                            ArrayType array = new ArrayType(ReadTypeSignature());

                            uint rank = ReadCompressedUInt32();

                            uint[] sizes = new uint[ReadCompressedUInt32()];
                            for (int i = 0; i < sizes.Length; i++)
                                sizes[i] = ReadCompressedUInt32();

                            int[] lowBounds = new int[ReadCompressedUInt32()];
                            for (int i = 0; i < lowBounds.Length; i++)
                                lowBounds[i] = ReadCompressedInt32();

                            array.Dimensions.Clear();

                            for (int i = 0; i < rank; i++) {
                                int? lower = null, upper = null;

                                if (i < lowBounds.Length)
                                    lower = lowBounds[i];

                                if (i < sizes.Length)
                                    upper = lower + (int) sizes[i] - 1;

                                array.Dimensions.Add(new ArrayDimension(lower, upper));
                            }

                            return array;

                        case MetadataType.OptionalModifier:
                            return new OptionalModifierType(GetTypeDefOrRef(), ReadTypeSignature());

                        case MetadataType.RequiredModifier:
                            return new RequiredModifierType(GetTypeDefOrRef(), ReadTypeSignature());

                        case MetadataType.Sentinel:
                            return new SentinelType(ReadTypeSignature());

                        case MetadataType.Var:
                        case MetadataType.MVar:
                        case MetadataType.GenericInstance:
                            throw new NotSupportedException($"Unsupported generic callsite element: {etype}");

                        case MetadataType.Object:
                            return moduleTo.TypeSystem.Object;

                        case MetadataType.Void:
                            return moduleTo.TypeSystem.Void;

                        case MetadataType.TypedByReference:
                            return moduleTo.TypeSystem.TypedReference;

                        case MetadataType.IntPtr:
                            return moduleTo.TypeSystem.IntPtr;

                        case MetadataType.UIntPtr:
                            return moduleTo.TypeSystem.UIntPtr;

                        case MetadataType.Boolean:
                            return moduleTo.TypeSystem.Boolean;

                        case MetadataType.Char:
                            return moduleTo.TypeSystem.Char;

                        case MetadataType.SByte:
                            return moduleTo.TypeSystem.SByte;

                        case MetadataType.Byte:
                            return moduleTo.TypeSystem.Byte;

                        case MetadataType.Int16:
                            return moduleTo.TypeSystem.Int16;

                        case MetadataType.UInt16:
                            return moduleTo.TypeSystem.UInt16;

                        case MetadataType.Int32:
                            return moduleTo.TypeSystem.Int32;

                        case MetadataType.UInt32:
                            return moduleTo.TypeSystem.UInt32;

                        case MetadataType.Int64:
                            return moduleTo.TypeSystem.Int64;

                        case MetadataType.UInt64:
                            return moduleTo.TypeSystem.UInt64;

                        case MetadataType.Single:
                            return moduleTo.TypeSystem.Single;

                        case MetadataType.Double:
                            return moduleTo.TypeSystem.Double;

                        case MetadataType.String:
                            return moduleTo.TypeSystem.String;

                        default:
                            throw new NotSupportedException($"Unsupported callsite element: {etype}");
                    }
                }

            }
        }

    }
}
