using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Verse;

namespace Multiplayer.Compat
{
    static class ReferenceBuilder
    {
        private enum BuildResult
        {
            Success,
            Skipped,
            Failure,
        }

        public static (int successes, int skipped, int failures) Restore(string refsFolder)
        {
            var requestedFiles = Directory.CreateDirectory(refsFolder).GetFiles("*.txt");
            var successes = 0;
            var skipped = 0;
            var failures = 0;

            foreach (var request in requestedFiles)
            {
                switch (BuildReference(refsFolder, request))
                {
                    case BuildResult.Success:
                        successes++;
                        break;
                    case BuildResult.Skipped:
                        skipped++;
                        break;
                    case BuildResult.Failure:
                    default:
                        failures++;
                        break;
                }
            }

            return (successes, skipped, failures);
        }

        private static BuildResult BuildReference(string refsFolder, FileInfo request)
        {
            var asmId = Path.GetFileNameWithoutExtension(request.Name);
            Log.Warning($"Trying to write:\nFile: {request.FullName}\nAssembly: {asmId}");

            var assembly = LoadedModManager.RunningModsListForReading
                .SelectMany(m => ModContentPack.GetAllFilesForModPreserveOrder(m, "Assemblies/", f => f.ToLower() == ".dll"))
                .FirstOrDefault(f => f.Item2.Name == asmId + ".dll")?.Item2;

            if (assembly == null)
            {
                Log.Warning("Null assembly found, skipping");
                return BuildResult.Failure;
            }

            var hash = ComputeHash(assembly.FullName);
            var hashFile = Path.Combine(refsFolder, asmId + ".txt");

            if (File.Exists(hashFile) && File.ReadAllText(hashFile) == hash)
            {
                Log.Warning($"Hashes are matching, skipping. Hash: {hash}");
                return BuildResult.Skipped;
            }

            Log.Warning($"MpCompat References: Writing {asmId}.dll");

            var outFile = Path.Combine(refsFolder, asmId + ".dll");
            var asmDef = AssemblyDefinition.ReadAssembly(assembly.FullName);

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
            return BuildResult.Success;
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