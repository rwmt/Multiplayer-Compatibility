---
name: rimworld-mp-compat
description: Implement, review, and test RimWorld Multiplayer Compatibility patches, or investigate multiplayer desync archives. Use for synchronization and determinism work in the MPCompat project, not general gameplay advice.
---

# RimWorld Multiplayer compatibility

Produce a patch or diagnosis grounded in the supported target-mod and Multiplayer
versions. MP runs a lockstep simulation: synchronize player inputs and make the
resulting simulation deterministic. Do not repair divergent peers by copying
state from the host.

This skill is self-contained. It requires no other installed skill, paid service,
particular editor, or game-control harness. Source paths in the references are
relative to the relevant repository root, not this skill's installation folder.
When used in a checkout, also read its `AGENTS.md` and contributor instructions.

## Start with the evidence

Establish the requested mod/action, supported RimWorld and MP versions, target
mod build, relevant dependencies, and the reported reproduction or archive.
Inspect available source and assemblies before asking for missing details.
Separate a released-build requirement from an explicitly requested development
stack. An unavailable build or game environment limits the verdict; it does not
justify inventing API behavior or claiming a test passed.

Trace the real UI entry point, validation, confirmation/cancel path, state
mutation, and cleanup. Identify whether the defect belongs to MPCompat, the
target mod, a framework, or Multiplayer itself. Keep work within the requested
repositories and publication scope; report an external dependency if it cannot
be fixed there.

## Choose the relevant reference

| Work | Read |
| --- | --- |
| New patch, sync boundary, RNG fix, or review | [Patching patterns](references/patching.md) and analogous patches in both source projects |
| Desync archive or uncertain first divergence | [Desync investigation](references/desyncs.md) |
| Build, reference refresh, or deployment | [Build and deployment](references/build.md) |
| Manual or automated host/client verification | [Testing and evidence](references/testing.md) |
| Set up or use GABS/RimBridge automation | [Optional automation setup](references/automation.md), bundled configuration, scenarios, and companion source |

Load only the references needed for the task. Verify version-sensitive details
against the actual implementation rather than treating examples as fixed API
contracts.

## Development loop

Prefer native method/property registration, field watching, lambda/delegate
support, designators, and built-in serializers. Add a wrapper or custom worker
only for a demonstrated missing capability. Preserve the whole native action
and its lifecycle. A method's visibility alone is not a reason to replace it.

Classify RNG by its actual caller. Simulation-context `Verse.Rand` normally
already participates in lockstep. Trace the first divergent input or RNG state
before introducing isolation or seeding. A tick-time call to a registered sync
method is not a network broadcast.

Implement a coherent batch, build the affected projects, then test the changed
behavior. Retain valid evidence for unchanged paths; expand testing when shared
dependencies or new failures warrant it. Recheck affected consumers after a
serializer or shared-helper change. Do not repeat a live matrix for prose edits.

Deliver the focused diff with the tested versions, commands/scenarios, outcomes,
and remaining gaps. Distinguish a hypothesis from a confirmed cause, a build
from an MP pass, and merged source from an available release. Successful QA does
not expand permission to push, open/update an upstream PR, or merge.
