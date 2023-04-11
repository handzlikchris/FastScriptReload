using System;
using System.Collections.Generic;
using ImmersiveVRTools.Editor.Common.WelcomeScreen.PreferenceDefinition;

namespace FastScriptReload.Editor
{
    [Serializable]
    public class FileWatcherSetupEntry: JsonObjectListSerializable<FileWatcherSetupEntry>
    { 
        public string path;
        public string filter;
        public bool includeSubdirectories;

        public FileWatcherSetupEntry(string path, string filter, bool includeSubdirectories)
        {
            this.path = path;
            this.filter = filter;
            this.includeSubdirectories = includeSubdirectories;
        }

        [Obsolete("Serialization required")]
        public FileWatcherSetupEntry()
        {
        }

        public override List<IJsonObjectRepresentationRenderingInfo> GenerateRenderingInfo()
        {
            return new List<IJsonObjectRepresentationRenderingInfo>
            {
                new JsonObjectRepresentationStringRenderingInfo<FileWatcherSetupEntry>("Path", (e) => e.path, (o, val) => o.path = val, 230),
                new JsonObjectRepresentationStringRenderingInfo<FileWatcherSetupEntry>("Filter", (e) => e.filter, (o, val) => o.filter = val, 100),
                new JsonObjectRepresentationBoolRenderingInfo<FileWatcherSetupEntry>("Include Subdirectories", (e) => e.includeSubdirectories, (o, val) => o.includeSubdirectories = val, 145),
            };
        }
    }
}