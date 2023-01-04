using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq.Expressions;
using MonoMod.Utils;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;

namespace MonoMod.Utils {
    // The following mostly qualifies as r/badcode material.
    internal static partial class _DMDEmit {

        // Mono
        private static readonly MethodInfo _ILGen_make_room =
            typeof(ILGenerator).GetMethod("make_room", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _ILGen_emit_int =
            typeof(ILGenerator).GetMethod("emit_int", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _ILGen_ll_emit =
            typeof(ILGenerator).GetMethod("ll_emit", BindingFlags.NonPublic | BindingFlags.Instance);

        // .NET
        private static readonly MethodInfo _ILGen_EnsureCapacity =
            typeof(ILGenerator).GetMethod("EnsureCapacity", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _ILGen_PutInteger4 =
            typeof(ILGenerator).GetMethod("PutInteger4", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _ILGen_InternalEmit =
            typeof(ILGenerator).GetMethod("InternalEmit", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _ILGen_UpdateStackSize =
            typeof(ILGenerator).GetMethod("UpdateStackSize", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo f_DynILGen_m_scope =
            typeof(ILGenerator).Assembly
            .GetType("System.Reflection.Emit.DynamicILGenerator")?.GetField("m_scope", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo f_DynScope_m_tokens =
            typeof(ILGenerator).Assembly
            .GetType("System.Reflection.Emit.DynamicScope")?.GetField("m_tokens", BindingFlags.NonPublic | BindingFlags.Instance);

        // Based on https://referencesource.microsoft.com/#mscorlib/system/reflection/mdimport.cs,74bfbae3c61889bc
        private static readonly Type[] CorElementTypes = new Type[] {
            null,
            typeof(void),
            typeof(bool),
            typeof(char),
            typeof(sbyte),
            typeof(byte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(string),
            typeof(IntPtr)
        };

        internal static void _EmitCallSite(DynamicMethod dm, ILGenerator il, System.Reflection.Emit.OpCode opcode, CallSite csite) {
            /* The mess in this method is heavily based off of the code available at the following links:
             * https://github.com/Microsoft/referencesource/blob/3b1eaf5203992df69de44c783a3eda37d3d4cd10/mscorlib/system/reflection/emit/dynamicmethod.cs#L791
             * https://github.com/Microsoft/referencesource/blob/3b1eaf5203992df69de44c783a3eda37d3d4cd10/mscorlib/system/reflection/emit/dynamicilgenerator.cs#L353
             * https://github.com/mono/mono/blob/82e573122a55482bf6592f36f819597238628385/mcs/class/corlib/System.Reflection.Emit/DynamicMethod.cs#L411
             * https://github.com/mono/mono/blob/82e573122a55482bf6592f36f819597238628385/mcs/class/corlib/System.Reflection.Emit/ILGenerator.cs#L800
             * https://github.com/dotnet/coreclr/blob/0fbd855e38bc3ec269479b5f6bf561dcfd67cbb6/src/System.Private.CoreLib/src/System/Reflection/Emit/SignatureHelper.cs#L57
             */

            List<object> _tokens = null;
            int _GetTokenForType(Type v) {
                _tokens.Add(v.TypeHandle);
                return _tokens.Count - 1 | 0x02000000 /* (int) MetadataTokenType.TypeDef */;
            }
            int _GetTokenForSig(byte[] v) {
                _tokens.Add(v);
                return _tokens.Count - 1 | 0x11000000 /* (int) MetadataTokenType.Signature */;
            }
#if !NETSTANDARD
            DynamicILInfo _info = null;
            if (ReflectionHelper.IsMono) {
                // GetDynamicILInfo throws "invalid signature" in .NET - let's hope for the best for mono...
                _info = dm.GetDynamicILInfo();
            } else {
#endif
                // For .NET, we need to access DynamicScope m_scope and its List<object> m_tokens
                _tokens = f_DynScope_m_tokens.GetValue(f_DynILGen_m_scope.GetValue(il)) as List<object>;
#if !NETSTANDARD
            }

            int GetTokenForType(Type v) => _info != null ? _info.GetTokenFor(v.TypeHandle) : _GetTokenForType(v);
            int GetTokenForSig(byte[] v) => _info != null ? _info.GetTokenFor(v) : _GetTokenForSig(v);

#else
            int GetTokenForType(Type v) => _GetTokenForType(v);
            int GetTokenForSig(byte[] v) => _GetTokenForSig(v);
#endif

            byte[] signature = new byte[32];
            int currSig = 0;
            int sizeLoc = -1;

            // This expects a MdSigCallingConvention
            AddData((byte) csite.CallingConvention);
            sizeLoc = currSig++;

            List<Type> modReq = new List<Type>();
            List<Type> modOpt = new List<Type>();

            ResolveWithModifiers(csite.ReturnType, out Type returnType, out Type[] returnTypeModReq, out Type[] returnTypeModOpt, modReq, modOpt);
            AddArgument(returnType, returnTypeModReq, returnTypeModOpt);

            foreach (ParameterDefinition param in csite.Parameters) {
                if (param.ParameterType.IsSentinel)
                    AddElementType(0x41 /* CorElementType.Sentinel */);

                if (param.ParameterType.IsPinned) {
                    AddElementType(0x45 /* CorElementType.Pinned */);
                    // AddArgument(param.ParameterType.ResolveReflection());
                    // continue;
                }

                ResolveWithModifiers(param.ParameterType, out Type paramType, out Type[] paramTypeModReq, out Type[] paramTypeModOpt, modReq, modOpt);
                AddArgument(paramType, paramTypeModReq, paramTypeModOpt);
            }

            AddElementType(0x00 /* CorElementType.End */);

            // For most signatures, this will set the number of elements in a byte which we have reserved for it.
            // However, if we have a field signature, we don't set the length and return.
            // If we have a signature with more than 128 arguments, we can't just set the number of elements,
            // we actually have to allocate more space (e.g. shift everything in the array one or more spaces to the
            // right.  We do this by making a copy of the array and leaving the correct number of blanks.  This new
            // array is now set to be m_signature and we use the AddData method to set the number of elements properly.
            // The forceCopy argument can be used to force SetNumberOfSignatureElements to make a copy of
            // the array.  This is useful for GetSignature which promises to trim the array to be the correct size anyway.

            byte[] temp;
            int newSigSize;
            int currSigHolder = currSig;

            // We need to have more bytes for the size.  Figure out how many bytes here.
            // Since we need to copy anyway, we're just going to take the cost of doing a
            // new allocation.
            if (csite.Parameters.Count < 0x80) {
                newSigSize = 1;
            } else if (csite.Parameters.Count < 0x4000) {
                newSigSize = 2;
            } else {
                newSigSize = 4;
            }

            // Allocate the new array.
            temp = new byte[currSig + newSigSize - 1];

            // Copy the calling convention.  The calling convention is always just one byte
            // so we just copy that byte.  Then copy the rest of the array, shifting everything
            // to make room for the new number of elements.
            temp[0] = signature[0];
            Buffer.BlockCopy(signature, sizeLoc + 1, temp, sizeLoc + newSigSize, currSigHolder - (sizeLoc + 1));
            signature = temp;

            //Use the AddData method to add the number of elements appropriately compressed.
            currSig = sizeLoc;
            AddData(csite.Parameters.Count);
            currSig = currSigHolder + (newSigSize - 1);

            // This case will only happen if the user got the signature through 
            // InternalGetSignature first and then called GetSignature.
            if (signature.Length > currSig) {
                temp = new byte[currSig];
                Array.Copy(signature, temp, currSig);
                signature = temp;
            }

            // Emit.

            if (_ILGen_emit_int != null) {
                // Mono
                _ILGen_make_room.Invoke(il, new object[] { 6 });
                _ILGen_ll_emit.Invoke(il, new object[] { opcode });
                _ILGen_emit_int.Invoke(il, new object[] { GetTokenForSig(signature) });

            } else {
                // .NET
                _ILGen_EnsureCapacity.Invoke(il, new object[] { 7 });
                _ILGen_InternalEmit.Invoke(il, new object[] { opcode });

                // The only IL instruction that has VarPop behaviour, that takes a
                // Signature token as a parameter is calli.  Pop the parameters and
                // the native function pointer.  To be conservative, do not pop the
                // this pointer since this information is not easily derived from
                // SignatureHelper.
                if (opcode.StackBehaviourPop == System.Reflection.Emit.StackBehaviour.Varpop) {
                    // Pop the arguments and native function pointer off the stack.
                    _ILGen_UpdateStackSize.Invoke(il, new object[] { opcode, -csite.Parameters.Count - 1 });
                }

                _ILGen_PutInteger4.Invoke(il, new object[] { GetTokenForSig(signature) });
            }

            void AddArgument(Type clsArgument, Type[] requiredCustomModifiers, Type[] optionalCustomModifiers) {
                if (optionalCustomModifiers != null)
                    foreach (Type t in optionalCustomModifiers)
                        InternalAddTypeToken(GetTokenForType(t), 0x20 /* CorElementType.CModOpt */);

                if (requiredCustomModifiers != null)
                    foreach (Type t in requiredCustomModifiers)
                        InternalAddTypeToken(GetTokenForType(t), 0x1F /* CorElementType.CModReqd */);

                AddOneArgTypeHelper(clsArgument);
            }

            void AddData(int data) {
                // A managed representation of CorSigCompressData; 

                if (currSig + 4 > signature.Length) {
                    signature = ExpandArray(signature);
                }

                if (data <= 0x7F) {
                    signature[currSig++] = (byte) (data & 0xFF);
                } else if (data <= 0x3FFF) {
                    signature[currSig++] = (byte) ((data >> 8) | 0x80);
                    signature[currSig++] = (byte) (data & 0xFF);
                } else if (data <= 0x1FFFFFFF) {
                    signature[currSig++] = (byte) ((data >> 24) | 0xC0);
                    signature[currSig++] = (byte) ((data >> 16) & 0xFF);
                    signature[currSig++] = (byte) ((data >> 8) & 0xFF);
                    signature[currSig++] = (byte) ((data) & 0xFF);
                } else {
                    throw new ArgumentException("Integer or token was too large to be encoded.");
                }
            }

            byte[] ExpandArray(byte[] inArray, int requiredLength = -1) {
                if (requiredLength < inArray.Length)
                    requiredLength = inArray.Length * 2;

                byte[] outArray = new byte[requiredLength];
                Buffer.BlockCopy(inArray, 0, outArray, 0, inArray.Length);
                return outArray;
            }

            void AddElementType(byte cvt) {
                // Adds an element to the signature.  A managed represenation of CorSigCompressElement
                if (currSig + 1 > signature.Length)
                    signature = ExpandArray(signature);

                signature[currSig++] = cvt;
            }

            void AddToken(int token) {
                // A managed represenation of CompressToken
                // Pulls the token appart to get a rid, adds some appropriate bits
                // to the token and then adds this to the signature.

                int rid = (token & 0x00FFFFFF); //This is RidFromToken;
                int type = (token & unchecked((int) 0xFF000000)); //This is TypeFromToken;

                if (rid > 0x3FFFFFF) {
                    // token is too big to be compressed    
                    throw new ArgumentException("Integer or token was too large to be encoded.");
                }

                rid = (rid << 2);

                // TypeDef is encoded with low bits 00  
                // TypeRef is encoded with low bits 01  
                // TypeSpec is encoded with low bits 10    
                if (type == 0x01000000 /* MetadataTokenType.TypeRef */) {
                    //if type is mdtTypeRef
                    rid |= 0x1;
                } else if (type == 0x1b000000 /* MetadataTokenType.TypeSpec */) {
                    //if type is mdtTypeSpec
                    rid |= 0x2;
                }

                AddData(rid);
            }

            void InternalAddTypeToken(int clsToken, byte CorType) {
                // Add a type token into signature. CorType will be either CorElementType.Class or CorElementType.ValueType
                AddElementType(CorType);
                AddToken(clsToken);
            }

            void AddOneArgTypeHelper(Type clsArgument) { AddOneArgTypeHelperWorker(clsArgument, false); }
            void AddOneArgTypeHelperWorker(Type clsArgument, bool lastWasGenericInst) {
                if (clsArgument.IsGenericType && (!clsArgument.IsGenericTypeDefinition || !lastWasGenericInst)) {
                    AddElementType(0x15 /* CorElementType.GenericInst */);

                    AddOneArgTypeHelperWorker(clsArgument.GetGenericTypeDefinition(), true);

                    Type[] genargs = clsArgument.GetGenericArguments();

                    AddData(genargs.Length);

                    foreach (Type t in genargs)
                        AddOneArgTypeHelper(t);
                } else if (clsArgument.IsByRef) {
                    AddElementType(0x10 /* CorElementType.ByRef */);
                    clsArgument = clsArgument.GetElementType();
                    AddOneArgTypeHelper(clsArgument);
                } else if (clsArgument.IsPointer) {
                    AddElementType(0x0F /* CorElementType.Ptr */);
                    AddOneArgTypeHelper(clsArgument.GetElementType());
                } else if (clsArgument.IsArray) {
#if false
                        if (clsArgument.IsArray && clsArgument == clsArgument.GetElementType().MakeArrayType()) { // .IsSZArray unavailable.
                            AddElementType(0x1D /* CorElementType.SzArray */);

                            AddOneArgTypeHelper(clsArgument.GetElementType());
                        } else
#endif
                    {
                        AddElementType(0x14 /* CorElementType.Array */);

                        AddOneArgTypeHelper(clsArgument.GetElementType());

                        // put the rank information
                        int rank = clsArgument.GetArrayRank();
                        AddData(rank);     // rank
                        AddData(0);     // upper bounds
                        AddData(rank);  // lower bound
                        for (int i = 0; i < rank; i++)
                            AddData(0);
                    }
                } else {
                    // This isn't 100% accurate, but... oh well.
                    byte type = 0; // 0 is reserved anyway.

                    for (int i = 0; i < CorElementTypes.Length; i++) {
                        if (clsArgument == CorElementTypes[i]) {
                            type = (byte) i;
                            break;
                        }
                    }

                    if (type == 0) {
                        if (clsArgument == typeof(object)) {
                            type = 0x1C /* CorElementType.Object */;
                        } else if (clsArgument.IsValueType) {
                            type = 0x11 /* CorElementType.ValueType */;
                        } else {
                            // Let's hope for the best.
                            type = 0x12 /* CorElementType.Class */;
                        }
                    }

                    if (type <= 0x0E /* CorElementType.String */ ||
                        type == 0x16 /* CorElementType.TypedByRef */ ||
                        type == 0x18 /* CorElementType.I */ ||
                        type == 0x19 /* CorElementType.U */ ||
                        type == 0x1C /* CorElementType.Object */
                    ) {
                        AddElementType(type);
                    } else if (clsArgument.IsValueType) {
                        InternalAddTypeToken(GetTokenForType(clsArgument), 0x11 /* CorElementType.ValueType */);
                    } else {
                        InternalAddTypeToken(GetTokenForType(clsArgument), 0x12 /* CorElementType.Class */);
                    }
                }
            }

        }

    }
}
