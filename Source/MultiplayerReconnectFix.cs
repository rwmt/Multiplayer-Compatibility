using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Multiplayer.API;
using Steamworks;
using Verse;
using Verse.Steam;

namespace Multiplayer.Compat;

/// <summary>
/// Fixes RimWorld Multiplayer reconnect after "fix and restart" on mod mismatch.
/// Host stale pendingSteam blocks repeat Steam P2P approval; lingering join slots block the same username.
/// </summary>
public static class MultiplayerReconnectFix
{
    private const string RestartDelayFlag = "MultiplayerCompatRestartReconnect";
    private const int RestartReconnectDelayMs = 4000;

    private static readonly MethodInfo AcceptPlayerJoinRequest =
        AccessTools.Method("Multiplayer.Client.SteamIntegration:AcceptPlayerJoinRequest");

    private static readonly MethodInfo StopMultiplayerAndClearAllWindows =
        AccessTools.Method("Multiplayer.Client.Multiplayer:StopMultiplayerAndClearAllWindows");

    private static readonly MethodInfo JoinIfApplicableMethod =
        AccessTools.Method("Multiplayer.Client.Util.AutoJoinHandler:JoinIfApplicable");

    private static readonly Type MpType = AccessTools.TypeByName("Multiplayer.Client.Multiplayer");
    private static readonly Type ServerType = AccessTools.TypeByName("Multiplayer.Common.MultiplayerServer");
    private static readonly Type DisconnectReasonType = AccessTools.TypeByName("Multiplayer.Common.MpDisconnectReason");
    private static readonly Type PendingPlayerWindowType =
        AccessTools.TypeByName("Multiplayer.Client.Windows.PendingPlayerWindow");
    private static readonly Type RequestType =
        PendingPlayerWindowType?.GetNestedType("Request", BindingFlags.Public);

    private static MethodInfo _enqueueJoinRequest;
    private static Delegate _joinRequestCallback;
    private static bool _p2pHandlerRegistered;

    public static void Apply()
    {
        if (MpType == null || ServerType == null)
        {
            Log.Warning("MPCompat :: Multiplayer reconnect fix skipped (Multiplayer types not found).");
            return;
        }

        PatchServerUsername();
        PatchFixAndRestart();
        PatchRunCallback();
        PatchSetDisconnected();
        PatchAutoJoinHandler();
        LongEventHandler.ExecuteWhenFinished(RegisterP2PReconnectHandler);
        Log.Message("MPCompat :: Multiplayer reconnect-after-restart fix loaded.");
    }

    private static void PatchAutoJoinHandler()
    {
        if (JoinIfApplicableMethod == null)
            return;

        MpCompat.harmony.Patch(JoinIfApplicableMethod,
            prefix: new HarmonyMethod(typeof(MultiplayerReconnectFix), nameof(DelayRestartAutoJoin)));
    }

    /// <summary>Give the host time to drop the old Steam/MP slot before the client reconnects.</summary>
    private static bool DelayRestartAutoJoin()
    {
        if (Environment.GetEnvironmentVariable(RestartDelayFlag) is not "true")
            return true;

        Environment.SetEnvironmentVariable(RestartDelayFlag, "");
        LongEventHandler.QueueLongEvent(DeferredRestartJoin, "MpConnecting", false, null);
        return false;
    }

    private static void DeferredRestartJoin()
    {
        Thread.Sleep(RestartReconnectDelayMs);
        JoinIfApplicableMethod?.Invoke(null, null);
    }

    private static void PatchServerUsername()
    {
        var joiningState = AccessTools.TypeByName("Multiplayer.Common.ServerJoiningState");
        var handleUsername = AccessTools.DeclaredMethod(joiningState, "HandleUsername");
        if (handleUsername == null) return;

        MpCompat.harmony.Patch(handleUsername,
            prefix: new HarmonyMethod(typeof(MultiplayerReconnectFix), nameof(ReplaceStaleJoiningPlayer)));
    }

    private static void PatchFixAndRestart()
    {
        var doRestart = AccessTools.DeclaredMethod(
            AccessTools.TypeByName("Multiplayer.Client.JoinDataWindow+FixAndRestartWindow"), "DoRestart");
        if (doRestart == null) return;

        MpCompat.harmony.Patch(doRestart,
            prefix: new HarmonyMethod(typeof(MultiplayerReconnectFix), nameof(DisconnectBeforeFixAndRestart)),
            postfix: new HarmonyMethod(typeof(MultiplayerReconnectFix), nameof(MarkRestartReconnect)));
    }

    private static void PatchRunCallback()
    {
        var runCallback = AccessTools.DeclaredMethod(RequestType, "RunCallback");
        if (runCallback == null) return;

        MpCompat.harmony.Patch(runCallback,
            postfix: new HarmonyMethod(typeof(MultiplayerReconnectFix), nameof(CleanupPendingSteamOnReject)));
    }

    private static void PatchSetDisconnected()
    {
        var setDisconnected = AccessTools.DeclaredMethod(
            AccessTools.TypeByName("Multiplayer.Common.PlayerManager"), "SetDisconnected");
        if (setDisconnected == null) return;

        MpCompat.harmony.Patch(setDisconnected,
            postfix: new HarmonyMethod(typeof(MultiplayerReconnectFix), nameof(RemovePendingSteamOnDisconnect)));
    }

    private static void RegisterP2PReconnectHandler()
    {
        if (_p2pHandlerRegistered)
            return;

        if (!SteamManager.Initialized)
        {
            Log.Warning("MPCompat :: Steam not ready; retrying reconnect P2P handler next frame.");
            LongEventHandler.QueueLongEvent(RegisterP2PReconnectHandler, "MpCompatReconnect", false, null);
            return;
        }

        if (AcceptPlayerJoinRequest == null || RequestType == null || PendingPlayerWindowType == null)
            return;

        _enqueueJoinRequest ??= AccessTools.DeclaredMethod(PendingPlayerWindowType, "EnqueueJoinRequest",
            [typeof(CSteamID), typeof(Action<,>).MakeGenericType(RequestType, typeof(bool))]);

        if (_enqueueJoinRequest == null)
            return;

        _joinRequestCallback ??= Delegate.CreateDelegate(
            typeof(Action<,>).MakeGenericType(RequestType, typeof(bool)),
            typeof(MultiplayerReconnectFix).GetMethod(nameof(SteamJoinRequestCallback),
                BindingFlags.Static | BindingFlags.NonPublic));

        Callback<P2PSessionRequest_t>.Create(req => HandleP2PSessionRequest(req.m_steamIDRemote));
        _p2pHandlerRegistered = true;
        Log.Message("MPCompat :: Steam P2P reconnect handler registered.");
    }

    private static void DisconnectBeforeFixAndRestart() =>
        StopMultiplayerAndClearAllWindows?.Invoke(null, null);

    private static void MarkRestartReconnect() =>
        Environment.SetEnvironmentVariable(RestartDelayFlag, "true");

    private static void ReplaceStaleJoiningPlayer(object packet)
    {
        var server = AccessTools.Property(ServerType, "instance")?.GetValue(null);
        if (server == null) return;

        var username = AccessTools.Field(packet.GetType(), "username")?.GetValue(packet) as string;
        if (username.NullOrEmpty()) return;

        var existing = AccessTools.Method(ServerType, "GetPlayer", [typeof(string)])?.Invoke(server, [username]);
        if (existing == null) return;

        if (AccessTools.Property(existing.GetType(), "IsPlaying")?.GetValue(existing) is true)
            return;

        var steamId = AccessTools.Field(existing.GetType(), "steamId")?.GetValue(existing);
        if (steamId is ulong id && id != 0)
            CleanupHostSteamSlot((CSteamID)id);

        var disconnect = AccessTools.Method(existing.GetType(), "Disconnect", [DisconnectReasonType, typeof(byte[])]);
        var clientLeft = Enum.Parse(DisconnectReasonType, "ClientLeft");
        disconnect?.Invoke(existing, [clientLeft, null]);
    }

    private static void CleanupPendingSteamOnReject(object __instance, bool accepted)
    {
        if (accepted) return;

        if (TryGetSteamId(__instance, out var steamId))
            CleanupHostSteamSlot(steamId);
    }

    private static void RemovePendingSteamOnDisconnect(object conn)
    {
        var player = AccessTools.Field(conn.GetType(), "serverPlayer")?.GetValue(conn);
        if (player == null) return;

        var steamId = AccessTools.Field(player.GetType(), "steamId")?.GetValue(player);
        if (steamId is ulong id && id != 0)
            CleanupHostSteamSlot((CSteamID)id);
    }

    private static void HandleP2PSessionRequest(CSteamID remoteId)
    {
        if (!IsHostingSteam())
            return;

        var session = AccessTools.Field(MpType, "session")?.GetValue(null);
        if (session == null)
            return;

        var pendingSteam = AccessTools.Field(session.GetType(), "pendingSteam")?.GetValue(session) as IList;
        if (pendingSteam == null || !pendingSteam.Contains(remoteId))
            return;

        // Vanilla MP ignores repeat requests while pendingSteam still contains this Steam ID.
        pendingSteam.Remove(remoteId);
        CloseP2PSession(remoteId);
        ReenqueueSteamJoinRequest(remoteId);
    }

    private static bool IsHostingSteam()
    {
        var localServer = AccessTools.Property(MpType, "LocalServer")?.GetValue(null);
        if (localServer == null) return false;

        var settings = AccessTools.Field(localServer.GetType(), "settings")?.GetValue(localServer);
        return settings != null && AccessTools.Field(settings.GetType(), "steam")?.GetValue(settings) is true;
    }

    private static void ReenqueueSteamJoinRequest(CSteamID remoteId)
    {
        var mpSettings = AccessTools.Field(MpType, "settings")?.GetValue(null);
        if (mpSettings != null &&
            AccessTools.Field(mpSettings.GetType(), "autoAcceptSteam")?.GetValue(mpSettings) is true)
        {
            AcceptPlayerJoinRequest?.Invoke(null, [remoteId]);
            return;
        }

        var session = AccessTools.Field(MpType, "session")?.GetValue(null);
        var pendingSteam = AccessTools.Field(session?.GetType() ?? typeof(object), "pendingSteam")
            ?.GetValue(session) as IList;

        if (pendingSteam != null && !pendingSteam.Contains(remoteId))
            pendingSteam.Add(remoteId);

        _enqueueJoinRequest?.Invoke(null, [remoteId, _joinRequestCallback]);

        var knownUsers = AccessTools.Field(session!.GetType(), "knownUsers")?.GetValue(session) as IList;
        if (knownUsers != null && !knownUsers.Contains(remoteId))
            knownUsers.Add(remoteId);

        AccessTools.Method(session.GetType(), "NotifyChat")?.Invoke(session, null);
        SteamFriends.RequestUserInformation(remoteId, true);
    }

    private static void SteamJoinRequestCallback(object joinReq, bool accepted)
    {
        if (!accepted)
        {
            if (TryGetSteamId(joinReq, out var steamId))
                CleanupHostSteamSlot(steamId);
            return;
        }

        if (TryGetSteamId(joinReq, out var id))
            AcceptPlayerJoinRequest?.Invoke(null, [id]);
    }

    private static void CleanupHostSteamSlot(CSteamID steamId)
    {
        RemoveFromPendingSteam(steamId);
        CloseP2PSession(steamId);
    }

    private static void RemoveFromPendingSteam(CSteamID steamId)
    {
        var session = AccessTools.Field(MpType, "session")?.GetValue(null);
        (AccessTools.Field(session?.GetType() ?? typeof(object), "pendingSteam")?.GetValue(session) as IList)
            ?.Remove(steamId);
    }

    private static void CloseP2PSession(CSteamID steamId)
    {
        if (!SteamManager.Initialized)
            return;

        try
        {
            SteamNetworking.CloseP2PSessionWithUser(steamId);
        }
        catch (Exception ex)
        {
            Log.Warning($"MPCompat :: CloseP2PSessionWithUser failed for {steamId}: {ex.Message}");
        }
    }

    private static bool TryGetSteamId(object request, out CSteamID steamId)
    {
        steamId = default;
        var nullable = AccessTools.Field(request.GetType(), "steamId")?.GetValue(request);
        if (nullable == null)
            return false;

        var nullableType = nullable.GetType();
        if (!(bool)nullableType.GetProperty("HasValue")!.GetValue(nullable)!)
            return false;

        steamId = (CSteamID)nullableType.GetProperty("Value")!.GetValue(nullable)!;
        return true;
    }
}
