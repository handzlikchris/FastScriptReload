using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    sealed class WeakReferenceComparer : EqualityComparer<WeakReference> {

        public override bool Equals(WeakReference x, WeakReference y)
            => ReferenceEquals(x.SafeGetTarget(), y.SafeGetTarget()) && x.SafeGetIsAlive() == y.SafeGetIsAlive();

        public override int GetHashCode(WeakReference obj)
            => obj.SafeGetTarget()?.GetHashCode() ?? 0;

    }
}
