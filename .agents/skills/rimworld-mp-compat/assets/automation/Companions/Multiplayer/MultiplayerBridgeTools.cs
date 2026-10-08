using System;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LudeonTK;
using Multiplayer.Client;
using Multiplayer.Client.Util;
using Multiplayer.Common;
using RimBridgeServer.Sdk;
using Verse;

namespace MPCompat.Multiplayer.BridgeTools;

public sealed class MultiplayerBridgeTools
{
    [Tool("mpcompat/multiplayer/host_current_game",
        Description = "Host the currently loaded single-player game through Multiplayer on an explicit direct endpoint.",
        ResultDescription = "Returns whether Multiplayer accepted the host request and the initial session state.")]
    public static Task<object> HostCurrentGame(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        [ToolParameter(Description = "Direct bind endpoint; use 127.0.0.1:30502 for an isolated local test")] string endpoint = "127.0.0.1:30502",
        [ToolParameter(Description = "Name shown for the automated test session")] string gameName = "MPCompat automated test")
    {
        return ctx.MainThread.InvokeAsync<object>(() =>
        {
            if (Current.Game == null)
                return new { success = false, message = "Load a game before hosting." };
            if (global::Multiplayer.Client.Multiplayer.session != null || global::Multiplayer.Client.Multiplayer.LocalServer != null)
                return new { success = false, message = "A Multiplayer session or local server already exists.", state = DescribeState() };

            var settings = new ServerSettings
            {
                gameName = gameName,
                direct = true,
                directAddress = endpoint,
                lan = false,
                steam = false,
                syncConfigs = true,
                desyncTraces = true,
                debugMode = true,
                devModeScope = DevModeScope.Everyone,
                pauseOnDesync = true,
                pauseOnJoin = true
            };

            var accepted = HostWindow.HostProgrammatically(settings);
            return new
            {
                success = accepted,
                message = accepted ? "Multiplayer accepted the host request." : "Multiplayer rejected the host request.",
                endpoint,
                state = DescribeState()
            };
        }, cancellationToken);
    }

    [Tool("mpcompat/multiplayer/get_session_state",
        Description = "Read a compact Multiplayer connection and local-server state snapshot.",
        ResultDescription = "Returns whether this process is hosting or connected and the current client state.")]
    public static Task<object> GetSessionState(IRimBridgeContext ctx, CancellationToken cancellationToken)
    {
        return ctx.MainThread.InvokeAsync<object>(DescribeState, cancellationToken);
    }

    [Tool("mpcompat/multiplayer/set_synced_god_mode",
        Description = "Set the initiating Multiplayer player's god-mode state through Multiplayer's registered sync method.",
        ResultDescription = "Returns the requested per-player state; advance Multiplayer ticks before relying on it in a command.")]
    public static Task<object> SetSyncedGodMode(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        [ToolParameter(Description = "True to enable god mode for the initiating player")] bool enabled = true)
    {
        return ctx.MainThread.InvokeAsync<object>(() =>
        {
            var session = global::Multiplayer.Client.Multiplayer.session;
            var gameComp = global::Multiplayer.Client.Multiplayer.GameComp;
            if (global::Multiplayer.Client.Multiplayer.Client == null || session == null || gameComp == null)
                return new { success = false, message = "No active Multiplayer session." };
            if (!gameComp.debugMode)
                return new { success = false, message = "The Multiplayer session does not have synchronized debug mode enabled." };

            DebugSettings.godMode = enabled;
            gameComp.SetGodMode(session.playerId, enabled);
            return new
            {
                success = true,
                message = "God-mode state was queued through Multiplayer; advance ticks before issuing the dependent command.",
                playerId = session.playerId,
                requestedGodMode = enabled
            };
        }, cancellationToken);
    }

    [Tool("mpcompat/multiplayer/enter_synced_debug_action",
        Description = "Enter an exact RimWorld debug-action tree path through Multiplayer's native debug synchronization wrapper.",
        ResultDescription = "Returns the resolved node and whether it opened a map tool awaiting a synchronized cell click.")]
    public static Task<object> EnterSyncedDebugAction(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        [ToolParameter(Description = "Exact path such as Actions\\Spawn thing...\\WoodLog")] string path)
    {
        return ctx.MainThread.InvokeAsync<object>(() =>
        {
            if (global::Multiplayer.Client.Multiplayer.Client == null)
                return new { success = false, message = "No active Multiplayer client connection." };
            if (!global::Multiplayer.Client.Multiplayer.GameComp.debugMode)
                return new { success = false, message = "The Multiplayer session does not have synchronized debug mode enabled." };
            if (string.IsNullOrWhiteSpace(path))
                return new { success = false, message = "path is required." };

            Dialog_Debug.TrySetupNodeGraph();
            var node = Dialog_Debug.rootNode;
            foreach (var segment in path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                node.TrySetupChildren();
                var matches = node.children
                    .Where(child => string.Equals(child.label, segment, StringComparison.Ordinal)
                        || string.Equals(child.LabelNow, segment, StringComparison.Ordinal))
                    .ToList();
                if (matches.Count != 1)
                {
                    return new
                    {
                        success = false,
                        message = $"Expected one exact child '{segment}' below '{node.Path}', found {matches.Count}.",
                        candidates = node.children.Select(child => child.LabelNow).OrderBy(label => label).ToList()
                    };
                }
                node = matches[0];
            }

            if (!node.VisibleNow || !node.ActiveNow)
                return new { success = false, message = $"Debug action '{path}' is not active in the current game view." };

            node.Enter(null);
            return new
            {
                success = true,
                message = DebugTools.curTool == null
                    ? "Debug action entered through Multiplayer synchronization."
                    : "Synchronized debug map tool is awaiting a cell click.",
                resolvedPath = node.Path,
                actionType = node.actionType.ToString(),
                awaitingMapClick = DebugTools.curTool != null
            };
        }, cancellationToken);
    }

    [Tool("mpcompat/multiplayer/play_ticks",
        Description = "Advance a synchronous Multiplayer session through its global time-speed command, then pause it.",
        ResultDescription = "Returns the observed Multiplayer timer range and whether the requested tick count completed.")]
    public static async Task<object> PlayTicks(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        [ToolParameter(Description = "Number of Multiplayer simulation ticks to advance")] int ticks = 300,
        [ToolParameter(Description = "Wall-clock timeout in milliseconds")] int timeoutMs = 30000,
        [ToolParameter(Description = "Simulation speed: Normal, Fast, or Superfast")] string speed = "Normal")
    {
        if (ticks <= 0 || ticks > 60000)
            return new { success = false, message = "ticks must be between 1 and 60000." };
        if (timeoutMs < 1000 || timeoutMs > 300000)
            return new { success = false, message = "timeoutMs must be between 1000 and 300000." };
        if (!Enum.TryParse(speed, true, out TimeSpeed parsedSpeed)
            || parsedSpeed is TimeSpeed.Paused or TimeSpeed.Ultrafast)
            return new { success = false, message = "speed must be Normal, Fast, or Superfast." };

        var start = await ctx.MainThread.InvokeAsync(() =>
        {
            if (global::Multiplayer.Client.Multiplayer.Client == null)
                return new TickStart(false, "No active Multiplayer client connection.", 0);
            if (global::Multiplayer.Client.Multiplayer.GameComp.asyncTime)
                return new TickStart(false, "This first automation version supports synchronous time only.", TickPatch.Timer);

            global::Multiplayer.Client.Multiplayer.Client.SendCommand(
                CommandType.GlobalTimeSpeed, ScheduledCommand.Global, (byte)parsedSpeed);
            return new TickStart(true, string.Empty, TickPatch.Timer);
        }, cancellationToken);
        if (!start.success)
            return new { success = false, message = start.message, startTick = start.tick };

        var stopwatch = Stopwatch.StartNew();
        var currentTick = start.tick;
        try
        {
            while (currentTick - start.tick < ticks && stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                await ctx.Game.FramesAsync(1, cancellationToken);
                currentTick = await ctx.MainThread.InvokeAsync(() => TickPatch.Timer, cancellationToken);
            }
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                global::Multiplayer.Client.Multiplayer.Client?.SendCommand(
                    CommandType.GlobalTimeSpeed, ScheduledCommand.Global, (byte)TimeSpeed.Paused);
            }, cancellationToken);
        }

        var completed = currentTick - start.tick >= ticks;
        return new
        {
            success = completed,
            message = completed ? "Requested Multiplayer ticks completed and pause was requested." : "Timed out before the requested tick count completed; pause was requested.",
            startTick = start.tick,
            endTick = currentTick,
            advancedTicks = currentTick - start.tick,
            requestedTicks = ticks,
            elapsedMs = stopwatch.ElapsedMilliseconds
        };
    }

    private static object DescribeState()
    {
        var multiplayer = global::Multiplayer.Client.Multiplayer.session;
        var client = global::Multiplayer.Client.Multiplayer.Client;
        var localServer = global::Multiplayer.Client.Multiplayer.LocalServer;
        return new
        {
            success = true,
            sessionExists = multiplayer != null,
            isHosting = localServer != null,
            localServerRunning = localServer?.running ?? false,
            clientExists = client != null,
            clientState = client?.State.ToString() ?? "None",
            gameName = multiplayer?.gameName ?? string.Empty,
            desynced = multiplayer?.desynced ?? false
        };
    }

    private sealed class TickStart
    {
        public bool success { get; }
        public string message { get; }
        public int tick { get; }

        public TickStart(bool success, string message, int tick)
        {
            this.success = success;
            this.message = message;
            this.tick = tick;
        }
    }
}
