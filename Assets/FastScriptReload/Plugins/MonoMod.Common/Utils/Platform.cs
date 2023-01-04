using System;

namespace MonoMod.Utils {
    /// <summary>
    /// Generic platform enum.
    /// </summary>
    [Flags]
    public enum Platform : int {
        /// <summary>
        /// Bit applied to all OSes (Unknown, Windows, MacOS, ...). 
        /// </summary>
        OS = 1 << 0,

        /// <summary>
        /// On demand 64-bit platform bit.
        /// </summary>
        Bits64 = 1 << 1,

        /// <summary>
        /// Applied to all NT and NT-oid platforms (Windows).
        /// </summary>
        NT = 1 << 2,
        /// <summary>
        /// Applied to all Unix and Unix-oid platforms (macOS, Linux, ...).
        /// </summary>
        Unix = 1 << 3,

        /// <summary>
        /// On demand ARM platform bit.
        /// </summary>
        ARM = 1 << 16,

        /// <summary>
        /// On demand Wine bit. DON'T RELY ON THIS.
        /// </summary>
        Wine = 1 << 17,

        /// <summary>
        /// Unknown OS.
        /// </summary>
        Unknown = OS | (1 << 4),
        /// <summary>
        /// Windows, using the NT kernel.
        /// </summary>
        Windows = OS | NT | (1 << 5),
        /// <summary>
        /// macOS, using the Darwin kernel.
        /// </summary>
        MacOS = OS | Unix | (1 << 6),
        /// <summary>
        /// Linux.
        /// </summary>
        Linux = OS | Unix | (1 << 7),
        /// <summary>
        /// Android, using the Linux kernel.
        /// </summary>
        Android = Linux | (1 << 8),
        /// <summary>
        /// iOS, sharing components with macOS.
        /// </summary>
        iOS = MacOS | (1 << 9),
    }
}
