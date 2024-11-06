using AmongUs.GameOptions;
using Hazel;
using System.Collections.Generic;
using TONX.Roles.Core;
using UnityEngine;
using static TONX.Translator;

namespace TONX.Roles.Crewmate;
public sealed class Mortician : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Mortician),
            player => new Mortician(player),
            CustomRoles.Mortician,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            22400,
            null,
            "mo|入殮師|入检师|入殓",
            "#333c49"
        );
    public Mortician(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        LastPlayerName = new();
        MsgToSend = null;
    }

    private static Dictionary<byte, string> LastPlayerName;
    private string MsgToSend;
    private void SendRPC(bool add, Vector3 loc = new())
    {
        using var sender = CreateSender();
        sender.Writer.Write(add);
        if (add)
        {
            sender.Writer.Write(loc.x);
            sender.Writer.Write(loc.y);
            sender.Writer.Write(loc.z);
        }
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        if (reader.ReadBoolean())
            LocateArrow.Add(Player.PlayerId, new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        else LocateArrow.RemoveAllTarget(Player.PlayerId);
    }
    public override void OnPlayerDeath(PlayerControl player, CustomDeathReason deathReason, bool isOnMeeting = false)
    {
        var target = player;
        var pos = target.GetTruePosition();
        float minDis = float.MaxValue;
        string minName = string.Empty;
        foreach (var pc in Main.AllAlivePlayerControls)
        {
            if (pc.PlayerId == target.PlayerId) continue;
            var dis = Vector2.Distance(pc.GetTruePosition(), pos);
            if (dis < minDis && dis < 1.5f)
            {
                minDis = dis;
                minName = pc.GetRealName();
            }
        }

        LastPlayerName.TryAdd(target.PlayerId, minName);
        LocateArrow.Add(Player.PlayerId, target.transform.position);
        SendRPC(true, target.GetTruePosition());
    }
    public override void OnStartMeeting()
    {
        LocateArrow.RemoveAllTarget(Player.PlayerId);
        SendRPC(false);
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (reporter == null || !Is(reporter) || target == null || reporter.PlayerId == target.PlayerId) return;
        MsgToSend = LastPlayerName.TryGetValue(target.PlayerId, out var name)
            ? string.Format(GetString("MorticianGetInfo"), target.PlayerName, name)
            : string.Format(GetString("MorticianGetNoInfo"), target.PlayerName);
    }
    public override void NotifyOnMeetingStart(ref List<(string, byte, string)> msgToSend)
    {
        if (MsgToSend != null)
            msgToSend.Add((MsgToSend, Player.PlayerId, Utils.ColorString(RoleInfo.RoleColor, GetString("MorticianCheckTitle"))));
        MsgToSend = null;
    }
    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer) || !(seen) || isForMeeting) return "";
        return (Utils.ColorString(Color.white, LocateArrow.GetArrows(seer)));
    }
}