# Desync investigation

Find the earliest demonstrated divergence and its owning layer. The first
reported RNG mismatch is often a later consumer of state that already differs.
Do not patch that consumer merely because it is visible in the trace.

## Read the archive

Inspect archive entries before assuming a format; versions differ. Common files:

| File | Use |
| --- | --- |
| `desync_info` | Versions, metadata, and peer identity |
| `local_traces.txt`, `host_traces.txt` | Compare aligned RNG events and stacks |
| `local_logs.txt` | Exceptions, loader failures, and warnings |
| `local_metadata.txt` | Mod list and settings |
| `local_jitted_methods.txt`, `host_jitted_methods.txt` | Supporting clues about compiled code paths |

JIT inventories are not execution traces, per-tick histories, or call counts.
Differences can guide investigation but do not prove a divergent simulation path.
Absence alone does not exonerate a mod: inlining and collection differences can
limit what is recorded. Confirm with source, logs, or targeted probes.

## Walk backwards from the mismatch

1. Establish the actual versions, peer identities, settings, and reproduction.
   Check registration failures and exceptions before assuming an RNG defect.
2. Align trace events by tick and the format's available identifiers. Account for
   missing/truncated traces; a raw line offset is not necessarily the same event.
3. Compare the earliest mismatch and its stacks. Distinguish the same call with
   different input/state from a different call path or extra RNG consumption.
4. Trace the values read by that call to their producers. Inspect earlier
   unsynchronized inputs, exception recovery, background completion, iteration
   order, cache rebuilds, and external RNG.
5. Verify the proposed cause in the supported source or assembly and reproduce
   the causal path. Keep observations, hypotheses, and confirmed causes distinct.

For example, an exception handler can remove a hediff on one peer, changing a
later candidate list and RNG consumption. Seeding the later random choice would
hide that symptom while leaving the hediff mismatch in place. Investigate the
exception and state transition instead. Likewise, a shuffled cache rebuilt at
different times needs its lifecycle examined before imposing a sort or seed.

## Probe without perturbing the result

If existing traces cannot reveal the divergent input, add bounded diagnostic
instrumentation in a disposable test build. Record matching tick, map/entity ID,
arguments, and pre/post state. Do not consume RNG, mutate simulation state, or
rely on wall-clock log order to align peers. Make nested-call state per invocation
so a probe does not overwrite its own observations.

For RNG probes, inspect the target version's state representation rather than
assuming private fields or a compressed bit layout. Equal incoming RNG state
does not prove equal arguments or game state; compare consumption and outgoing
state too. Different incoming state means the divergence occurred earlier.

Before concluding a patch never fires, verify the loaded DLL and hash, registration,
and the real caller. Consider inlining and logging visibility only after those
checks. Keep probes and companion tools out of the production diff unless they
are explicitly part of the requested deliverable.

When evidence remains insufficient, state the exact missing trace, version,
fixture, or observation and continue useful checks that do not depend on it.
Do not claim floating-point drift without identifying the first divergent
operation and matching inputs. Do not add speculative RNG isolation to close an
inconclusive investigation. Validate a confirmed fix using [host/client tests](testing.md).
