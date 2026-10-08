# Multiplayer testing and evidence

Manual host/client testing is valid. Automation is optional and should exercise
the same native actions and observe the same outcomes. No particular harness or
paid tool is required. If two peers are unavailable, finish available static and
build checks and report live MP coverage as not run.

For a reproducible GABS/RimBridge setup, use the optional
[automation guide](automation.md). It includes a two-process configuration,
health-report scenarios, and a Multiplayer companion. These tools support the
checks below; their successful execution is not itself a compatibility verdict.

## Select meaningful cases

Map each changed boundary to a semantic scenario. Cover action and reversal or
cleanup when both are affected. For UI/session changes, include open, close,
reopen, cancel, and relevant transitions. Repeat issuer-sensitive actions from
host and client. Include save/load, rejoin, cross-map, held-object, or world-object
cases when the patch changes those lifecycles or serialization paths.

Pin game/mod versions, ordered mod lists, and tested binaries. Use isolated
saved-data folders and logs for two processes on one machine, with distinct MP
usernames. Use matching relevant settings and record how they are shared; do not
force player defaults as a compatibility fix. Reuse fixtures only when their
versions and state meet the scenario's assumptions.

## Run the scenario

1. Check startup logs for loading, registration, and transpiler errors. Record a
   log position before the action so new errors can be attributed.
2. Host and join normally. Wait for an active host, a client actually playing,
   loaded map/world state, and settled pause. Confirm neither peer is desynced.
   A queued host/join request is not proof of readiness.
3. After joining, create shared fixtures through synchronized gameplay or MP's
   synchronized debug actions. Never spawn or modify simulation state separately
   on one process to prepare a positive test.
4. Exercise the action through its real UI entry point, preserving the interface
   context that triggers MP synchronization. Directly invoking the final mutation
   only tests that mutation, not the synchronization boundary.
5. Advance the shared MP simulation, then pause and wait for it to settle. Record
   actual tick progress. Never advance just one process with a local time API.
6. Assert the expected shared outcome on both peers using stable IDs and values
   relevant to the action, such as quantities, reservations, positions, session
   membership, or quest state. A local window need not appear on both peers.
7. Allow a delayed detection window appropriate to the affected behavior, then
   repeat state/desync checks. Several hundred ticks can be a useful initial
   window; longer jobs and lifecycle effects require their actual completion.
8. Preserve relevant logs and any `Desync-*.zip`. Capture screenshots when visual
   state matters, alongside semantic observations. A tool response or screenshot
   alone does not establish continued lockstep.

Queued commands may not execute while paused. Label immediate results as
submitted until ticks advance and their postconditions are observed. A timeout
also does not prove failure; inspect state before retrying a consuming action.

## Automation-specific checks

Use available automation to invoke native UI callbacks and read results, not to
perform the state mutation that the patch is meant to synchronize. Local camera,
selection, or window setup is fine when the final commit still uses the native
path. Debug tools must invoke MP's wrapped action, including the targeted click,
rather than an original unwrapped delegate. Per-player god mode also needs the
registered MP path before issuing a dependent command.

Repair a failed harness prerequisite before trusting dependent cases. For a new
or materially changed desync-detection harness, verify detection and archive
capture once with a known divergence in a disposable session. Keep this negative
control separate from positive tests. Do not repeat it for unrelated patch edits.

## Verdict and retesting

An MP pass requires the native action, expected shared outcome on both peers,
continued lockstep after the detection window, and no unexplained new relevant
errors. A successful build, initialization message, or single-player run is
insufficient. Attribute unrelated errors with baseline evidence instead of
silently excluding them. Registration failures invalidate affected scenarios.

Collect independent failures in a coherent sweep, but stop dependent tests when
a prerequisite fails. A desync invalidates later observations in that session;
preserve the archive and restart cleanly after the fix. Earlier independent
passes can remain valid if their code, dependencies, and assumptions are unchanged.

Retest changed behavior and affected shared consumers. Reuse valid current-build
cases in the final integration pass rather than rerunning the whole matrix after
every edit. Documentation edits and unchanged rebuilds do not themselves
invalidate live evidence. Add a focused SP regression when runtime patches can
affect single-player behavior.

Report each case as passed, failed, partial, blocked, or not run, with versions,
build identity, issuer, steps, observations, tick window, and any remaining gap.
Keep evidence concise and reproducible; full UI dumps are only useful when needed
to diagnose a particular failure.
