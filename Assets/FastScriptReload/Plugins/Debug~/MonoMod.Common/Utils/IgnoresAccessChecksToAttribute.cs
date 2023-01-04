namespace System.Runtime.CompilerServices {
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
#if !MONOMOD_INTERNAL
    public
#endif
    class IgnoresAccessChecksToAttribute : Attribute {
        public string AssemblyName { get; }
        public IgnoresAccessChecksToAttribute(string assemblyName) {
            AssemblyName = assemblyName;
        }
    }
}
