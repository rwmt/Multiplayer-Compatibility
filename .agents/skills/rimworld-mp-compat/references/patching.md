# Patching patterns and failure modes

## Source layout and registration

Check both `Source/Mods/` and `Source_Referenced/` for the mod, its dependencies,
and similar actions. Inspect `Source/MpCompat.cs`, `Source/MpCompatAttributes.cs`,
and `Source/PatchingUtilities.cs` for helpers before introducing a new one.
Use `[MpCompatFor("author.packageid")]` with the actual package ID and a
constructor accepting `ModContentPack`, following neighboring patches.

Thin optional-mod registrations usually fit `Source/Mods/`, using
`AccessTools.TypeByName` and named methods. Typed access in `Source_Referenced/`
can simplify substantial logic but adds reference maintenance. Do not move a
patch just to access a private method: direct registration supports private
methods. See [build guidance](build.md) before adding references.

Inspect initialization order before patching. Use
`LongEventHandler.ExecuteWhenFinished` for work that requires loaded play data
or later initialization; a method being patched can trigger its type's static
initializer. Check optional dependency presence rather than silently skipping a
required type/signature mismatch. Registration failures need investigation.

## Choose the action boundary

Prefer these approaches in order when they preserve the intended behavior:

1. Register the existing named method or supported property accessor with
   `MP.RegisterSyncMethod`. Check whether Multiplayer already registers it.
2. Register the real callback using `MpCompat.RegisterLambdaMethod` or
   `MpCompat.RegisterLambdaDelegate`, as appropriate to its target and captures.
3. Add a parameter transform or sync worker only for an unsupported instance,
   argument, or capture after inspecting the native serializer.
4. Use invocation hooks or redirect the final callback when local UI validation
   and confirmation must precede the synchronized commit.

Inspect compiler-generated callbacks in the supported assembly. Ordinals change
when lambdas are added or reordered; method and delegate registration are not
interchangeable. Record what each non-obvious ordinal represents and the version
checked in review evidence. If the callback merely forwards to a serializable
named method, register that method instead.

MP intercepts eligible interface calls and queues replay on all peers, including
the issuer. Surrounding UI code still runs locally. Calls during simulation run
locally rather than broadcasting a new command. Do not implement a host-only
tick decision followed by a supposedly synchronized result.

Preserve validation, eligibility, reservations, notifications, and cleanup. A
wrapper that updates a count but omits its transferable, cache, or notification
can leave peers inconsistent despite a plausible screen. Prefer calling the
mod's complete action over copying its body or dialog construction.

## Fields, selection, and sessions

For direct widget edits, inspect `MP.RegisterSyncField` and the
`MP.WatchBegin` / field `Watch` / `MP.WatchEnd` pattern in
`Source/Mods/CommonSense.cs`. Match begin/end paths, including guards and
exception behavior. Field watching is not a replacement for a multi-step action
whose invariants require the original commit method.

Add `SyncContext` only when the method consumes that context. `MapSelected`
captures map selection; it does not make arbitrary static selection lists
shared. A cross-map UI transition can change the selection before submission.
Capture the operation's objects and destination explicitly when needed, and
verify their map context in the serializer. Check loaded and unloaded destination
behavior if both are supported.

Keep purely local camera, selection, and window changes local. If a dialog
participates in a shared MP session, inspect native session support and existing
session patches. Test close, reopen, cancel, and cleanup; a visible window does
not prove that it references the correct shared session or objects.

## Serialization and reflection

Check the supported Multiplayer source, especially its sync registrations and
serializers, before adding custom serialization. Native support can resolve
things, pawns, and comps through their owning objects, but verify the actual
holder chain, including held or world objects used by this action.

For value-like `IExposable` arguments, inspect `.ExposeParameter(index)` and the
mod's `ExposeData`. It can preserve the runtime subtype without a hand-maintained
field list. It is not a substitute for resolving the identity of an existing
live object; collections also need their declared serialization path checked.

For a live child object needing a worker, reconstruct it from its stable owner
and public identity API. Avoid list indexes, process-local hashes, or whole-map
fallback scans unless their correctness is established. Review all consumers
before removing a shared worker, including delegate captures and watched fields.

Cache reflection used in per-frame or per-tick patches. Publicized target-mod
members can simplify the referenced project; do not assume this exposes MP
internals or makes them a stable API. Private MP-state access needs a concrete
reason and maintenance plan.

## RNG and other nondeterminism

Trace callers, not names: a smoke or fleck method can run from simulation.

| Actual context | Approach |
| --- | --- |
| Deterministic simulation using `Verse.Rand` | Preserve its normal stream consumption |
| Unsynchronized draw/UI method consuming `Verse.Rand` | Isolate the narrow consumer with the existing helper or a balanced scope |
| Simulation using external RNG | Inspect seeds and lifetime; use deterministic inputs/RNG where needed |
| Rebuilt cache whose order affects simulation | Trace rebuild timing and stable identity/order, including save/load |

`PatchSystemRand` and `PatchUnityRand` default to `patchPushPop: true`. Choose
that argument deliberately: simulation replacements generally need `false` so
they consume the lockstep stream. Check supported overloads and the actual
transpiled instructions; a helper name does not guarantee coverage.

`PatchPushPopRand` currently uses a prefix and postfix. Inspect exception paths
before relying on it to balance state. If a custom scope requires cleanup on
exceptions, pair push/pop using per-invocation Harmony state and a finalizer.
Do not hold RNG state across a window's lifetime or use one static slot for
nested calls.

Unseeded push/pop preserves the outer stream but does not make different incoming
states produce identical results. A stable seed is appropriate only when the
operation's lifecycle calls for an independent deterministic result. It is not
a fix for an unexplained earlier stream divergence. See [desyncs](desyncs.md).

Also inspect wall-clock inputs, iteration order, unstable IDs, and asynchronous
completion. Background work becomes a lockstep hazard when timing affects
simulation. Preserve the mod's stop/cancel/cleanup paths and cover every relevant
dispatcher; do not blanket-disable unrelated threads or rewrite their state.

Exception recovery that removes or partially mutates shared state can diverge
peers. Trace the original exception and partial effects instead of hiding it or
assuming logging fixes it. Treat floating-point drift as a hypothesis until the
first divergent operation and matching inputs are demonstrated across peers.
