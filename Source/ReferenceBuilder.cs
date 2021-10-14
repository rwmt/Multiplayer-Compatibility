using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Verse;

namespace Multiplayer.Compat
{
    static class ReferenceBuilder
    {
        public static void Restore(string refsFolder)
        {
            var requestedFiles = Directory.CreateDirectory(refsFolder).GetFiles("*.txt");

            foreach(var request in requestedFiles)
            {
                BuildReference(refsFolder, request);
            }
        }

        static void BuildReference(string refsFolder, FileInfo request)
        {
            var asmId = Path.GetFileNameWithoutExtension(request.Name);

            var assembly = LoadedModManager.RunningModsListForReading
                .SelectMany(m => ModContentPack.GetAllFilesForModPreserveOrder(m, "Assemblies/", f => f.ToLower() == ".dll"))
                .FirstOrDefault(f => f.Item2.Name == asmId + ".dll")?.Item2;
            
            var hash = ComputeHash(assembly.FullName);
            var hashFile = Path.Combine(refsFolder, asmId + ".txt");

            if (File.Exists(hashFile) && File.ReadAllText(hashFile) == hash)
                return;
            
            Console.WriteLine($"MpCompat References: Writing {asmId}.dll");
            
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
                    m.Body = new Mono.Cecil.Cil.MethodBody(m);
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

        static string ComputeHash(string assemblyPath)
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