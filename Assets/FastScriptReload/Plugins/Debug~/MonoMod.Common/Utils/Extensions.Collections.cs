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
using System.Collections;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    static partial class Extensions {

        /// <summary>
        /// See <see cref="List{T}.AddRange(IEnumerable{T})"/>
        /// </summary>
        public static void AddRange<T>(this Collection<T> list, IEnumerable<T> other) {
            foreach (T entry in other)
                list.Add(entry);
        }
        /// <summary>
        /// See <see cref="List{T}.AddRange(IEnumerable{T})"/>
        /// </summary>
        public static void AddRange(this IDictionary dict, IDictionary other) {
            foreach (DictionaryEntry entry in other)
                dict.Add(entry.Key, entry.Value);
        }
        /// <summary>
        /// See <see cref="List{T}.AddRange(IEnumerable{T})"/>
        /// </summary>
        public static void AddRange<K, V>(this IDictionary<K, V> dict, IDictionary<K, V> other) {
            foreach (KeyValuePair<K, V> entry in other)
                dict.Add(entry.Key, entry.Value);
        }
        /// <summary>
        /// See <see cref="List{T}.AddRange(IEnumerable{T})"/>
        /// </summary>
        public static void AddRange<K, V>(this Dictionary<K, V> dict, Dictionary<K, V> other) {
            foreach (KeyValuePair<K, V> entry in other)
                dict.Add(entry.Key, entry.Value);
        }

        /// <summary>
        /// See <see cref="List{T}.InsertRange(int, IEnumerable{T})"/>
        /// </summary>
        public static void InsertRange<T>(this Collection<T> list, int index, IEnumerable<T> other) {
            foreach (T entry in other)
                list.Insert(index++, entry);
        }

    }
}
