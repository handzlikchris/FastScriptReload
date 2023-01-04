using System;
using System.Reflection;
using System.Linq.Expressions;
using MonoMod.Utils;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Reflection.Emit;

namespace MonoMod.Utils {
#pragma warning disable IDE1006 // Naming Styles
    internal interface _IDMDGenerator {
#pragma warning restore IDE1006 // Naming Styles
        MethodInfo Generate(DynamicMethodDefinition dmd, object context);
    }
    /// <summary>
    /// A DynamicMethodDefinition "generator", responsible for generating a runtime MethodInfo from a DMD MethodDefinition.
    /// </summary>
    /// <typeparam name="TSelf"></typeparam>
#if !MONOMOD_INTERNAL
    public
#endif
    abstract class DMDGenerator<TSelf> : _IDMDGenerator where TSelf : DMDGenerator<TSelf>, new() {

        private static TSelf _Instance;

        protected abstract MethodInfo _Generate(DynamicMethodDefinition dmd, object context);

        MethodInfo _IDMDGenerator.Generate(DynamicMethodDefinition dmd, object context) {
            return _Postbuild(_Generate(dmd, context));
        }

        public static MethodInfo Generate(DynamicMethodDefinition dmd, object context = null)
            => _Postbuild((_Instance ?? (_Instance = new TSelf()))._Generate(dmd, context));

        internal static unsafe MethodInfo _Postbuild(MethodInfo mi) {
            if (mi == null)
                return null;

            if (ReflectionHelper.IsMono) {
                // Luckily we're guaranteed to be safe from DynamicMethod -> RuntimeMethodInfo conversions.
                if (!(mi is DynamicMethod) && mi.DeclaringType != null) {
                    // get_Assembly is virtual in some versions of Mono (notably older ones and the infamous Unity fork).
                    // ?. results in a call instead of callvirt to skip a redundant nullcheck, which breaks this on ^...
                    Module module = mi?.Module;
                    if (module == null)
                        return mi;
                    Assembly asm = module.Assembly; // Let's hope that this doesn't get optimized into a call.
                    Type asmType = asm?.GetType();
                    if (asmType == null)
                        return mi;

                    asm.SetMonoCorlibInternal(true);
                }
            }

            return mi;
        }

    }
}
