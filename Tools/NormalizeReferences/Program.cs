using Mono.Cecil;

if (args.Length != 3) throw new ArgumentException("Usage: NormalizeReferences input-directory output-directory RimWorld-directory");
Directory.CreateDirectory(args[1]);
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(args[0]);
resolver.AddSearchDirectory(args[2]);
resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
using var stubs = AssemblyDefinition.ReadAssembly(Path.Combine(args[2], "UnityStubs.dll"));
foreach (var path in Directory.GetFiles(args[0], "*.dll"))
{
    using var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver });
    foreach (var reference in assembly.MainModule.AssemblyReferences)
    {
        // Match the game's WaylithClr.AssemblyNameRedirects. These are build-only
        // copies; never install the normalized reference assemblies as mods.
        if (!reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal) &&
            reference.Name != "Assembly-CSharp-firstpass") continue;
        reference.Name = stubs.Name.Name;
        reference.Version = stubs.Name.Version;
        reference.PublicKeyToken = stubs.Name.PublicKeyToken;
    }
    // Mono placed Queue<T> in mscorlib; .NET exposes it through System.Collections.
    var collections = AssemblyNameReference.Parse("System.Collections, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
    foreach (var type in assembly.MainModule.GetTypeReferences())
        if (type.FullName == "System.Collections.Generic.Queue`1")
        {
            if (!assembly.MainModule.AssemblyReferences.Contains(collections))
                assembly.MainModule.AssemblyReferences.Add(collections);
            type.Scope = collections;
        }
    assembly.Write(Path.Combine(args[1], Path.GetFileName(path)));
}
Console.WriteLine("Normalized build references for Waylith.");
