using System;
using System.Reflection;
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
        /// Creates a delegate of the specified type from this method.
        /// </summary>
        /// <param name="method">The method to create the delegate from.</param>
        /// <typeparam name="T">The type of the delegate to create.</typeparam>
        /// <returns>The delegate for this method.</returns>
        public static T CreateDelegate<T>(this MethodBase method) where T : Delegate
            => (T) CreateDelegate(method, typeof(T), null);
        /// <summary>
        /// Creates a delegate of the specified type with the specified target from this method.
        /// </summary>
        /// <param name="method">The method to create the delegate from.</param>
        /// <typeparam name="T">The type of the delegate to create.</typeparam>
        /// <param name="target">The object targeted by the delegate.</param>
        /// <returns>The delegate for this method.</returns>
        public static T CreateDelegate<T>(this MethodBase method, object target) where T : Delegate
            => (T) CreateDelegate(method, typeof(T), target);
        /// <summary>
        /// Creates a delegate of the specified type from this method.
        /// </summary>
        /// <param name="method">The method to create the delegate from.</param>
        /// <param name="delegateType">The type of the delegate to create.</param>
        /// <returns>The delegate for this method.</returns>
        public static Delegate CreateDelegate(this MethodBase method, Type delegateType)
            => CreateDelegate(method, delegateType, null);
        /// <summary>
        /// Creates a delegate of the specified type with the specified target from this method.
        /// </summary>
        /// <param name="method">The method to create the delegate from.</param>
        /// <param name="delegateType">The type of the delegate to create.</param>
        /// <param name="target">The object targeted by the delegate.</param>
        /// <returns>The delegate for this method.</returns>
        public static Delegate CreateDelegate(this MethodBase method, Type delegateType, object target) {
            if (!typeof(Delegate).IsAssignableFrom(delegateType))
                throw new ArgumentException("Type argument must be a delegate type!");
            if (method is System.Reflection.Emit.DynamicMethod dm)
                return dm.CreateDelegate(delegateType, target);

#if NETSTANDARD
            // Built-in CreateDelegate is available in .NET Standard
            if (method is System.Reflection.MethodInfo mi)
                return mi.CreateDelegate(delegateType, target);
#endif

            RuntimeMethodHandle handle = method.MethodHandle;
            RuntimeHelpers.PrepareMethod(handle);
            IntPtr ptr = handle.GetFunctionPointer();
            return (Delegate) Activator.CreateInstance(delegateType, target, ptr);
        }

    }
}
