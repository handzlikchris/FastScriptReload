using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq.Expressions;
using MonoMod.Utils;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Security;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    class DynamicMethodReference : MethodReference {
        public MethodInfo DynamicMethod;

        public DynamicMethodReference(ModuleDefinition module, MethodInfo dm)
            : base("", module.TypeSystem.Void) {
            DynamicMethod = dm;
        }
    }
}
