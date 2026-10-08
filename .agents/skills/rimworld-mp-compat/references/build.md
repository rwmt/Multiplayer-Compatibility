# Build, references, and deployment

## Build from the checkout

Inspect the solution and both `.csproj` files for framework, language version,
NuGet packages, references, and any local copy targets. Use an SDK capable of
building that configuration and install the required .NET Framework reference
assemblies for the platform. Do not hardcode a contributor's SDK or game path.

From the repository root, build the affected project(s):

```sh
dotnet build Source/Multiplayer_Compat.csproj
dotnet build Source_Referenced/Multiplayer_Compat_Referenced.csproj
```

The referenced project also builds its project reference to the main project.
Current output roots are `Assemblies/` and `Referenced/`; inspect the actual
configuration-specific output location. Build both when shared helpers change.
Report restore/reference failures as such, separately from source errors.

## Target-mod references

The referenced project reads `References/*.dll` and uses Krafs.Publicizer for
configured assemblies. Inspect its `<Publicize>` entries and
`Source/MpCompatLoader.cs` before adding typed dependencies or helper types.
Optional mods must remain optional at runtime.

`Source/ReferenceBuilder.cs` and `Source/DebugActions.cs` implement the repository's
reference-generation path. A `References/<assembly-name>.txt` file requests a
reference for an installed assembly; generation strips method bodies, exposes
members, and records the source hash. These are build references, not playable
mod DLLs. Follow that mechanism and the existing reference conventions when
adding or refreshing committed stubs. Use the supported mod version and review
only the intended reference changes. Do not replace committed stubs with full
proprietary game or mod assemblies.

A build against a stub does not prove the deployed mod still has the expected
method body or lambda ordinal. Inspect the actual target assembly for those
checks. A merged upstream API change is not usable by released-build users
until it ships in the required dependency.

## Deploy a test build

Use a separate test profile or installation when available. Discover the actual
game/mod load paths from configuration and logs; a successful build or copy is
not evidence that the game loaded that DLL. Preserve the ordered mod list,
versions, fixture assumptions, and hashes of the tested assemblies on both peers.

Stop the test processes before replacing loaded DLLs. Back up files being
replaced and deploy only the intended artifacts. Check startup logs for loader,
registration, and transpiler failures. MPCompat's referenced assembly contains
many patches: a stale checkout can replace unrelated fixes when deployed.
When changing Multiplayer itself, keep its client/common/loader dependencies
mutually compatible rather than swapping a single DLL blindly.

An incremental build may reuse stale output after timestamp-preserving source
copies. Rebuild when that is a real concern, then check the relevant compiled
signature or registration as well as the deployed hash. A matching hash alone
does not establish that the binary reflects the intended source.

At handoff, restore temporary replacements and test configuration unless the
user requested retaining them. Check that the files are still the ones deployed
by the test before restoring; do not overwrite a concurrent Workshop or user
update. Keep backups, fixtures, and logs outside the product diff.
