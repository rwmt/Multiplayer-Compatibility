# Optional automated multiplayer QA

Use this guide when an agent should operate two real RimWorld peers. Manual
testing remains supported by [testing.md](testing.md). Installing the skill does
not install tools, register an MCP server, or launch games.

## Components and version baseline

| Component | Responsibility |
| --- | --- |
| [GABS](https://github.com/pardeike/GABS) | Start/stop isolated processes and expose their bridge tools through MCP |
| [RimBridgeServer](https://github.com/pardeike/RimBridgeServer) | In-game interaction, scripts, readiness, screenshots, logs, and evidence |
| Bundled Multiplayer BridgeTools | Host a session, inspect MP state, submit synchronized debug operations, and advance shared time |
| Agent and scenario-specific observations | Choose cases, exercise native actions, compare outcomes on both peers, and report a verdict |

The original workflow was exercised on Windows with RimWorld 1.6, GABS 1.1.0,
and RimBridgeServer 2.1.0. These are a reproducibility baseline, not a claim that
they are the latest versions. Obtain releases from the projects' release pages
([GABS](https://github.com/pardeike/GABS/releases),
[RimBridgeServer](https://github.com/pardeike/RimBridgeServer/releases)). Pin
versions during a comparison. Build the companion against the exact installed
Multiplayer and RimBridge SDK assemblies; it uses version-sensitive MP client
types. Repeat preflight after upgrades. Other platforms need their native GABS
binary and game launch paths; the Windows baseline does not establish their QA.

The assets are included in this skill so a copied installation remains usable:

- [GABS configuration template](../assets/automation/gabs-config.example.json)
- [Preflight report](../assets/automation/scenarios/preflight.json)
- [Session-health report](../assets/automation/scenarios/session-health.json)
- [Companion project](../assets/automation/Companions/Multiplayer/Multiplayer.BridgeTools.csproj)
  and [source](../assets/automation/Companions/Multiplayer/MultiplayerBridgeTools.cs)

## Local paths

Discover the installed tools and active mod locations from the local MCP/game
configuration and startup logs. Do not assume a drive, Steam library, Workshop
directory, or version subfolder. The commands below use PowerShell variables;
set them once in the current shell to the actual absolute paths:

| Variable | Local value |
| --- | --- |
| `$qaGabsExe` | GABS executable for this platform |
| `$qaGabsConfigDir` | External directory containing the prepared GABS `config.json` |
| `$qaRimBridgeSdk` | Installed `RimBridgeServer.Sdk.dll` |
| `$qaMultiplayerDll` | Installed `Multiplayer.dll` |
| `$qaMultiplayerCommonDll` | Matching installed `MultiplayerCommon.dll` |

These are shell variables, not GABS or MCP configuration features. JSON/TOML
examples require literal paths: replace their explicit placeholders with the
resolved values. Skill asset paths remain relative to this skill directory.

## 1. Install and prepare isolated profiles

Install RimBridgeServer as a mod, extract GABS to a tools directory, and verify
its version with `gabs version`. Use a local RimWorld installation that can run
two processes. Enable RimBridgeServer (`brrainz.rimbridgeserver`), Multiplayer,
MPCompat, the target mod, and their dependencies in both profiles, respecting
the mods' declared load order.

Create separate host and client saved-data folders, each with `Config/ModsConfig.xml`,
plus separate log paths. Start from a known working profile if useful, copying
configuration into the test folders rather than modifying the player's profile.
The peers need identical ordered mod lists and compatible settings/builds.
Enable developer mode in both test profiles and give the peers different
Multiplayer usernames before hosting/joining. The baseline stores MP settings
in `Config/Mod_2606448745_Multiplayer.xml`; confirm the actual file for the installed
version or set usernames through its settings UI. Saves live under the selected
profile's `Saves` directory.

Copy the GABS template to an external directory as `config.json`. Replace every
`REPLACE_WITH_...` value with an absolute local path: game executable and working
directory, host/client profiles, and host/client log files. Create their parent
directories. In JSON, use forward slashes or escape backslashes. Keep each launch
argument as a separate array entry; paths containing spaces do not need embedded
shell quotes. `-savedatafolder=...` is one argument. The client has a bounded
`connect` launch input that expands to Multiplayer's native `-connect` argument.

Run both configuration checks before launching, using the variables above:

```powershell
& $qaGabsExe games --configDir $qaGabsConfigDir doctor mp-host
& $qaGabsExe games --configDir $qaGabsConfigDir doctor mp-client
```

Doctor checks launch configuration; it does not prove that RimBridge loaded or
that the peers can join. Keep resolved config, profiles, logs, and GABS runtime
state outside the repository. GABS manages bridge endpoints and authentication;
do not copy one peer's cached bridge/runtime files into the other.

## 2. Connect the agent to GABS

Configure a local STDIO MCP server whose command is the absolute GABS executable
and whose arguments are `server`, `--configDir`, and the absolute configuration
directory. For example, Codex supports this entry in `~/.codex/config.toml`:

```toml
[mcp_servers.gabs]
command = 'REPLACE_WITH_ABSOLUTE_GABS_EXECUTABLE'
args = ['server', '--configDir', 'REPLACE_WITH_ABSOLUTE_GABS_CONFIG_DIRECTORY']
```

Replace the placeholders with the values of `$qaGabsExe` and `$qaGabsConfigDir`,
not the variable names; TOML does not interpolate these shell variables. Reload
the MCP connection after changing its setup.
See the [official MCP configuration documentation](https://developers.openai.com/codex/mcp)
for Codex, or your client's equivalent STDIO settings. Do not overwrite other
MCP entries. The template's normalization options support clients with strict
tool-name/schema handling.

Discover the connected tool schemas rather than guessing generated peer names.
GABS exposes `games_list`, `games_start`, `games_stop`, `games_tool_names`,
`games_tool_detail`, and `games_call_tool`. After a game's bridge connects, use
`games_tool_names` for that game ID, then inspect the tool detail before calling.
The `rimbridge/...` and `mpcompat/...` names below are original capability names;
the externally exposed name can include the peer prefix and normalization.

## 3. Build and deploy the Multiplayer companion

This is a separate test assembly, deliberately outside `Multiplayer_Compat.sln`.
Use an SDK that can build `net48` and the .NET Framework reference assemblies
required on your platform. The project takes three explicit assembly paths and
reports missing paths before compilation. It does not install or copy into the
game automatically.

From the skill directory, build with the resolved assembly paths:

```powershell
dotnet build ./assets/automation/Companions/Multiplayer/Multiplayer.BridgeTools.csproj -c Release `
  "-p:RimBridgeSdkPath=$qaRimBridgeSdk" `
  "-p:MultiplayerAssembly=$qaMultiplayerDll" `
  "-p:MultiplayerCommonAssembly=$qaMultiplayerCommonDll"
```

The build reports its output path beneath
`assets/automation/artifacts/BridgeTools/MPCompat.Multiplayer/`. With the test
processes stopped, deploy only `MPCompat.Multiplayer.BridgeTools.dll` into
`<RimWorld>/BridgeTools/MPCompat.Multiplayer/`. Do not deploy reference assemblies
or a private copy of `RimBridgeServer.Sdk.dll`. Record original files and deployed
hashes following [the deployment guidance](build.md).

BridgeTools discovery uses the shared game installation, not each profile.
Keep this companion installed only for tests whose mod list includes Multiplayer
and RimBridgeServer. On both peers, use `rimbridge/list_capabilities` and inspect
startup logs to verify these five tools registered:

| Capability suffix under `mpcompat/multiplayer/` | Behavior |
| --- | --- |
| `host_current_game` | Host a loaded SP fixture using MP's hosting API, defaulting to `127.0.0.1:30502`, with synchronized debug mode and desync traces |
| `get_session_state` | Read connection, host/server, and desync state |
| `set_synced_god_mode` | Submit the initiating player's MP god-mode change; ticks must advance before relying on it |
| `enter_synced_debug_action` | Enter an exact debug-tree path through MP's wrapper; targeted actions still require the wrapped map click |
| `play_ticks` | Submit global MP speed/pause commands and report observed timer progress; synchronous time only |

The companion hosts synchronous sessions from single-player fixtures. Do not use
it to convert an asynchronous MP save. `play_ticks` rejects asynchronous time;
an async-time scenario needs an appropriate MP-aware control and its own checks.

## 4. Run a scenario

1. Start `mp-host` through GABS and wait for its bridge. Run the bundled
   `preflight.json` with `rimbridge/run_script`, passing the file's JSON text as
   `scriptJson`. Inspect the result, mod order, logs, and companion registration.
2. Load a suitable SP save on the host and wait for a usable map/world. Call
   `mpcompat/multiplayer/host_current_game`. An accepted request is not readiness:
   poll `get_session_state` until `localServerRunning=true` and the host is playing.
3. Start `mp-client` with GABS `launchInputs: {"connect":"127.0.0.1:30502"}`.
   If already running from setup, stop that test client first; a second start
   does not apply new startup arguments to an existing process. Run preflight on
   the connected client. Require `sessionExists=true`, `clientExists=true`,
   `clientState="ClientPlaying"`, and `desynced=false` on both peers, plus the
   running host server and loaded usable game state. No session also reports
   `desynced=false`, so that field alone is not a readiness check.
4. Create fixtures after joining through synchronized gameplay, Architect
   designators, or wrapped debug tools. If needed, submit synchronized god mode
   and advance MP ticks before a dependent designator. Never use process-local
   spawning, god mode, or time controls to prepare a positive MP test.
5. Exercise the patched action through its real UI on the intended issuer.
   Call `play_ticks` from one peer, then observe a settled pause on both peers.
   It requests pause in cleanup; return from the tool does not prove that pause
   has executed. After a timeout/cancellation, inspect state before retrying or
   issuing a dependent action. Use MP's native synchronized pause if needed.
6. Assert scenario-specific shared outcomes on both peers using stable IDs and
   relevant state. Advance a delayed detection window (300 ticks is an initial
   smoke window, not a universal proof), then repeat state/desync checks.
7. Run `session-health.json` on each peer using `rimbridge/run_script` and
   `scriptJson`. It observes readiness with `pauseIfNeeded=false` to avoid local
   time changes. Inspect warnings, errors, shared state, and captures; preserve
   any `Desync-*.zip` in the isolated profile. Stop both test games through GABS
   after the sweep and restore temporary deployments.

These JSON scenarios collect reports. `continueOnError=false` stops failed
operations; it does not assert a gameplay outcome or that a returned
`desynced` field is false. The agent/test author must inspect results and supply
the semantic assertions. Follow [testing.md](testing.md) for negative controls,
issuer/lifecycle coverage, delayed detection, and retest selection.

For example, after building through a synchronized designator, compare the same
thing ID, definition, position, and relevant quantity on both peers before and
after the detection window. When the mod requires custom observations, add a
small separate BridgeTools extension that resolves stable objects, reads state,
or invokes the genuine UI action. Do not reproduce the gameplay mutation in a
helper and count that as a sync test. A direct API probe must be labeled as such.

## Troubleshooting and evidence

- No bridge/tools: check the isolated profile's active mod list and log, then
  the GABS connection state. `games_start` may return before connection finishes.
- Companion absent: verify DLL location, dependency versions, and registration
  errors. Rebuild against the deployed assemblies; initialization alone is not
  proof every tool registered.
- Join failure: check distinct usernames, identical mod order/builds, matching
  endpoint, and actual server readiness. Do not continue dependent scenarios.
- Blocked attention/errors: use diagnostic tools to read the error before
  acknowledging it. Attribute bootstrap/harness issues with baseline evidence;
  do not suppress them to manufacture a clean run.
- Action submitted while paused: advance shared MP ticks, then inspect the
  postcondition. A queued request or immediate screenshot is not execution.

Preserve the fixture identity, ordered mod list, versions, DLL hashes, per-peer
logs and relevant screenshots, semantic assertions, actual tick deltas, and
desync archives. Keep report artifacts outside the product diff. A build and
configuration check of these assets is not a new end-to-end run of the harness;
state exactly which live cases were executed on the final environment.
