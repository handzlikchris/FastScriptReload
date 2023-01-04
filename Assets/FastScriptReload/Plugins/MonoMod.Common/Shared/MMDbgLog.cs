// #define MONOMOD_DBGLOG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

// This class is included in every MonoMod assembly.
namespace MonoMod {
    internal static class MMDbgLog {

        public static readonly string Tag = typeof(MMDbgLog).Assembly.GetName().Name;

        public static TextWriter Writer;

        public static bool Debugging;

        static MMDbgLog() {
            bool enabled =
#if MONOMOD_DBGLOG
                true;
#else
                Environment.GetEnvironmentVariable("MONOMOD_DBGLOG") == "1" ||
                (Environment.GetEnvironmentVariable("MONOMOD_DBGLOG")?.ToLower(CultureInfo.InvariantCulture)?.Contains(Tag.ToLower(CultureInfo.InvariantCulture), StringComparison.Ordinal) ?? false);
#endif

            if (enabled)
                Start();
        }

        public static void WaitForDebugger() {
            // When in doubt, enable this debugging helper block, add Debugger.Break() where needed and attach WinDbg quickly.
            if (!Debugging) {
                Debugging = true;
                // WinDbg doesn't trigger Debugger.IsAttached
                Debugger.Launch();
                Thread.Sleep(6000);
                Debugger.Break();
            }
        }

        public static void Start() {
            if (Writer != null)
                return;

            string path = Environment.GetEnvironmentVariable("MONOMOD_DBGLOG_PATH");
            if (path == "-") {
                Writer = Console.Out;
                return;
            }

            if (string.IsNullOrEmpty(path))
                path = "mmdbglog.txt";
            path = Path.GetFullPath($"{Path.GetFileNameWithoutExtension(path)}-{Tag}{Path.GetExtension(path)}");

            try {
                if (File.Exists(path))
                    File.Delete(path);
            } catch { }
            try {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                Writer = new StreamWriter(new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete), Encoding.UTF8);
            } catch { }
        }

        public static void Log(string str) {
            TextWriter w = Writer;
            if (w == null)
                return;

            w.WriteLine(str);
            w.Flush();
        }

        public static T Log<T>(string str, T value) {
            TextWriter w = Writer;
            if (w == null)
                return value;

            w.WriteLine(string.Format(CultureInfo.InvariantCulture, str, value));
            w.Flush();
            return value;
        }

    }
}
