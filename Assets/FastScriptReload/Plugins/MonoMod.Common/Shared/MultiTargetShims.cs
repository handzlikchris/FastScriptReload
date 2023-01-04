using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using MonoMod.Utils;
using Mono.Cecil;

/* This class is included in every MonoMod assembly.
 * As far as I know, methods aren't guaranteed to be inlined
 * across assembly boundaries.
 * -ade
 */
static class MultiTargetShims {

    private static readonly object[] _NoArgs = new object[0];

    // Can't use the globalization-aware overloads on old .NET targets...
    // Weirdly enough this is very spotty, and the compiler will only *fall back* to extension methods,
    // thus keeping this un-#if'd is zero perf cost and zero maintenance cost.

    public static string Replace(this string self, string oldValue, string newValue, StringComparison comparison)
        => self.Replace(oldValue, newValue);

    public static bool Contains(this string self, string value, StringComparison comparison)
        => self.Contains(value);

    public static int GetHashCode(this string self, StringComparison comparison)
        => self.GetHashCode();

    public static int IndexOf(this string self, char value, StringComparison comparison)
        => self.IndexOf(value);

    public static int IndexOf(this string self, string value, StringComparison comparison)
        => self.IndexOf(value);


#if NETSTANDARD

    public static Module[] GetModules(this Assembly asm)
        => asm.Modules.ToArray();
    public static Module GetModule(this Assembly asm, string name)
        => asm.Modules.FirstOrDefault(module => module.Name == name);

    public static byte[] GetBuffer(this MemoryStream ms) {
        long posPrev = ms.Position;
        byte[] data = new byte[ms.Length];
        ms.Read(data, 0, data.Length);
        ms.Position = posPrev;
        return data;
    }

#endif

#if CECIL0_10
    public static TypeReference GetConstraintType(this TypeReference type)
        => type;
#else
    public static TypeReference GetConstraintType(this GenericParameterConstraint constraint)
        => constraint.ConstraintType;
#endif

}
