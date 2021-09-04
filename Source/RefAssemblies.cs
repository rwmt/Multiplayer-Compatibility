using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Verse;

namespace Multiplayer.Compat
{
    public static class RefAssemblies
    {
        [Conditional("DEBUG")]
        public static void Create(string refsFolder)
        {
            CreateRefAssembly(refsFolder, ModIds.RimFridgeID, ModIds.RimFridgeAsm);
        }

        private static void CreateRefAssembly(string refsFolder, string modId, string assemblyName)
        {
            Directory.CreateDirectory(refsFolder);
            
            var loadedMod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m =>
                m.PackageIdPlayerFacing.ToLowerInvariant() == modId);
            
            if (loadedMod == null)
                return;

            var asm = ModContentPack
                .GetAllFilesForModPreserveOrder(loadedMod, "Assemblies/", f => f.ToLower() == ".dll")
                .FirstOrDefault(f => f.Item2.Name == assemblyName + ".dll")?.Item2;

            if (asm == null)
            {
                Log.Warning($"MpCompat RefAssemblies: Mod {modId} is loaded but has no assembly {assemblyName}.dll");
                return;
            }

            var asmId = $"{modId.Replace('.', '_')}_{assemblyName}";
            var hash = ComputeHash(asm.FullName);
            var hashFile = Path.Combine(refsFolder, asmId + ".txt");

            if (File.Exists(hashFile) && File.ReadAllText(hashFile) == hash)
                return;
            
            Log.Message($"MpCompat RefAssemblies: Writing {assemblyName}.dll for {modId}");
            
            var outFile = Path.Combine(refsFolder, asmId + ".dll");
            var asmDef = AssemblyDefinition.ReadAssembly(asm.FullName);

            foreach (var t in asmDef.MainModule.GetTypes())
            {
                if (t.IsNested)
                    t.IsNestedPublic = true;
                else
                    t.IsPublic = true;

                foreach (var m in t.Methods)
                {
                    m.IsPublic = true;
                    m.Body = new MethodBody(m);
                }

                foreach (var f in t.Fields)
                {
                    f.IsInitOnly = false;
                    f.IsPublic = true;
                }
            }
            
            asmDef.Write(outFile);
            File.WriteAllText(hashFile, hash);
        }

        private static string ComputeHash(string assemblyPath)
        {
            var res = new StringBuilder();

            using var hash = SHA1.Create();
            using FileStream file = File.Open(assemblyPath, FileMode.Open, FileAccess.Read);
            
            hash.ComputeHash(file);
            
            foreach (byte b in hash.Hash)
                res.Append(b.ToString("X2"));

            return res.ToString();
        }
    }
}