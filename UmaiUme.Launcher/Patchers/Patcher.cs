using System.Collections.Generic;

namespace UmaiUme.Launcher.Patchers
{
    public abstract class Patcher
    {
        private List<string> patchAssemblies;
        public virtual string Name { get; }
        public virtual string Version { get; }

        public void AddPatchableAssembly(string name)
        {
            if (!patchAssemblies.Contains(name)) patchAssemblies.Add(name);
        }

        public virtual void LoadConfiguration()
        {
        }

        public virtual void Initialize()
        {
        }

        public virtual void PostPatch()
        {
        }

        public virtual void PrePatch()
        {
        }

        public abstract void LoadPatches();
        public abstract void Patch();
        public abstract void RestoreAssemblies();
    }
}