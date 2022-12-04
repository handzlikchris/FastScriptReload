namespace FastScriptReload.Runtime
{
    public class AssemblyChangesLoaderResolver
    {
        private static AssemblyChangesLoaderResolver _instance;
        public static AssemblyChangesLoaderResolver Instance => _instance ?? (_instance = new AssemblyChangesLoaderResolver());

        public IAssemblyChangesLoader Resolve()
        {
#if QuickCodeIteration_LoadAssemblyOverNetwork_Enabled
            return NetworkedAssemblyChangesSender.Instance;
#else
            return AssemblyChangesLoader.Instance;
#endif

        }
    }
}