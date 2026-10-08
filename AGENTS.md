# Multiplayer Compatibility contributor rules

These rules apply to LLM-assisted work throughout this repository. The reusable
[MP compatibility skill](.agents/skills/rimworld-mp-compat/SKILL.md) provides the
task workflow and references. Contributors remain responsible for reviewing the
generated patch and its evidence.

## Repository map

- `Source/Mods/`: optional-mod patches using runtime type discovery.
- `Source_Referenced/`: patches compiled against target-mod reference assemblies.
- `Source/MpCompat.cs`, `Source/MpCompatAttributes.cs`, and
  `Source/PatchingUtilities.cs`: existing registration and patching helpers.
- `Source/MpCompatLoader.cs`: patch discovery and conditional assembly loading.
- `References/`: build-time mod references and reference-generation hash files.
- `Assemblies/` and `Referenced/`: build outputs, not source changes to commit.
- `.agents/skills/rimworld-mp-compat/assets/automation/`: optional QA configuration,
  scenarios, and a separately built Multiplayer companion; not part of the mod's
  solution or release assemblies.

Search both source projects for related patches before choosing an approach.
Existing patches illustrate API usage; they do not prove that the same approach
is correct for a different call path or mod version.

## Patch rules

1. **Work from the actual versions.** Inspect target-mod source or the supported
   assembly, the Multiplayer implementation, and the project configuration.
   Verify signatures, lambda ordinals, serializers, and load timing. Do not
   invent APIs or assume that current source is already in a released mod.
2. **Synchronize inputs; preserve lockstep.** Register player actions at their
   interface boundary. A registered sync method called during simulation runs
   locally; it does not broadcast a host-only decision to the other peers.
3. **Preserve the complete action.** Prefer the mod's existing method, callback,
   property, or designator. Keep validation, confirmation/cancel behavior,
   reservations, notifications, and cleanup. Avoid copying a mod's transaction
   or synchronizing only its most visible field.
4. **Use native support first.** Check existing Multiplayer registrations and
   serializers before adding wrappers, transforms, or sync workers. Private
   methods can be registered directly. Reflection for optional-mod discovery is
   normal; private MP-state access needs a demonstrated gap in the public API.
5. **Keep local UI state out of simulation.** Capture the operation's inputs at
   the UI boundary. Selection, windows, camera, and static UI caches can differ
   between peers. Add sync context only for state the action actually reads.
6. **Trace RNG callers.** Ordinary simulation `Verse.Rand` belongs to lockstep.
   Isolate proven unsynchronized UI/render consumers, not every method that
   calls RNG. Do not seed a divergent consumer to hide an earlier divergence.
7. **Fix the owning layer.** Distinguish a mod's single-player bug, framework
   defect, MP-core issue, and compatibility interaction. Explain dependencies or
   temporary containment instead of silently replacing a root-cause fix with a
   compat workaround. Preserve player settings and single-player behavior.

## Validation and delivery

Read the skill's [build guidance](.agents/skills/rimworld-mp-compat/references/build.md)
before building or deploying. Build the affected project(s); changes to shared
helpers can affect both. A successful build proves neither registration success
nor multiplayer compatibility.

Changed sync boundaries, serializers, RNG behavior, or transpilers need relevant
host/client scenarios through the real action. Check the shared outcome on both
peers and allow delayed desync detection. Use the skill's
[testing guidance](.agents/skills/rimworld-mp-compat/references/testing.md).
If the environment cannot run them, report the exact untested cases. Never
label a build-only or single-player check as an MP pass. Documentation-only
changes need link/content checks, not a game launch.

Keep the diff focused. Do not include local paths, saves, logs, credentials,
temporary probes, generated game binaries, or unrelated reference refreshes.
Record target versions, build results, scenarios, limitations, and any required
unreleased dependencies in the PR or a requested audit artifact. Label cases as
passed, failed, partial, blocked, or not run; distinguish retained evidence from
a fresh test of the final build.

Preserve unrelated work and follow the requested branch and publication scope.
A push to a fork branch backing an upstream PR also updates that upstream PR.
Check the destination and existing PR before publishing. Do not infer permission
to publish upstream, comment, or merge from local implementation or successful QA.
