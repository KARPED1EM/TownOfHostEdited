using AmongUs.GameOptions;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using TONX.Modules;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using UnityEngine;

namespace TONX.Roles.Neutral;
public sealed class Pelican : RoleBase, IKiller, ISchrodingerCatOwner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Pelican),
            player => new Pelican(player),
            CustomRoles.Pelican,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            51200,
            SetupOptionItem,
            "pe|鵜鶘",
            "#34c84b",
            true,
            countType: CountTypes.Pelican
        );
    public Pelican(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        OriginalSpeed = new();
        CanVent = OptionCanVent.GetBool();
    }

    static OptionItem OptionKillCooldown;
    static OptionItem OptionCanVent;
    enum OptionName
    {
        PelicanKillCooldown
    }

    List<byte> EatenPlayers;
    Dictionary<byte, float> OriginalSpeed;
    Vector2 MyLastPos;

    public static bool CanVent;

    public SchrodingerCat.TeamType SchrodingerCatChangeTo => SchrodingerCat.TeamType.Pelican;

    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.PelicanKillCooldown, new(2.5f, 180f, 2.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, true, false);
    }
    public override void Add() => EatenPlayers = new();
    public bool IsKiller => false;
    public float CalculateKillCooldown()
    {
        if (!CanUseKillButton()) return 255f;
        return OptionKillCooldown.GetFloat();
    }
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);
    public bool CanUseSabotageButton() => false;
    public bool CanUseKillButton() => Player.IsAlive();
    private void SendRPC()
    {
        var sender = CreateSender();
        sender.Writer.Write(EatenPlayers.Count);
        EatenPlayers.Do(sender.Writer.Write);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        EatenPlayers = new();
        for (int i = 0; i < reader.ReadInt32(); i++)
            EatenPlayers.Add(reader.ReadByte());
    }
    public static bool IsEaten(byte id) => Main.AllPlayerControls.Any(p => p.GetRoleClass() is Pelican roleClass && (roleClass.EatenPlayers?.Contains(id) ?? false));
    public bool CanEat(byte id)
    {
        if (GameStates.IsMeeting) return false;
        var target = Utils.GetPlayerById(id);
        return target != null && target.IsAlive() && !target.inVent && !target.Is(CustomRoles.GM) && !IsEaten(id);
    }
    public override string GetProgressText(bool comms = false) => Utils.ColorString(EatenPlayers.Count >= 1 ? Utils.ShadeColor(RoleInfo.RoleColor, 0.25f) : Color.gray, $"({EatenPlayers.Count})");
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (info.IsSuicide) return true;
        if (!CanEat(target.PlayerId)) return false;
        Utils.TP(killer.NetTransform, target.GetTruePosition());
        EatPlayer(killer, target);
        killer.SetKillCooldownV2();
        killer.RPCPlayCustomSound("Eat");
        target.RPCPlayCustomSound("Eat");
        return false;
    }
    private void EatPlayer(PlayerControl killer, PlayerControl target)
    {
        OriginalSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId];
        EatenPlayers.Add(target.PlayerId);
        SendRPC();

        Utils.TP(target.NetTransform, Utils.GetBlackRoomPS());
        Main.AllPlayerSpeed[target.PlayerId] = 0.5f;
        ReportDeadBodyPatch.CanReport[target.PlayerId] = false;
        target.MarkDirtySettings();

        Utils.NotifyRoles(target);
        Utils.NotifyRoles(target);
        Logger.Info($"{killer.GetRealName()} 吞掉了 {target.GetRealName()}", "Pelican.OnCheckMurderAsKiller");
        return;
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo reportTarget)
    {
        foreach (var id in EatenPlayers)
        {
            var target = Utils.GetPlayerById(id);
            if (target == null) continue;

            Main.AllPlayerSpeed[id] = Main.AllPlayerSpeed[id] - 0.5f + OriginalSpeed[id];
            ReportDeadBodyPatch.CanReport[id] = true;

            target.RpcExileV2();
            target.SetRealKiller(Player);
            target.SetDeathReason(CustomDeathReason.Eaten);
            PlayerState.GetByPlayerId(id)?.SetDead();

            Logger.Info($"{Player.GetRealName()} 消化了 {target.GetRealName()}", "Pelican.OnReportDeadBody");
        }
        EatenPlayers = new();
        SendRPC();
    }
    public override void OnPlayerDeath(PlayerControl player, CustomDeathReason deathReason, bool isOnMeeting = false)
    {
        if (!Is(player)) return;
        foreach (var id in EatenPlayers)
        {
            var target = Utils.GetPlayerById(id);
            if (target == null) continue;

            Utils.TP(target.NetTransform, MyLastPos);
            Main.AllPlayerSpeed[id] = Main.AllPlayerSpeed[id] - 0.5f + OriginalSpeed[id];
            ReportDeadBodyPatch.CanReport[id] = true;

            target.MarkDirtySettings();
            RPC.PlaySoundRPC(id, Sounds.TaskComplete);
            Utils.NotifyRoles(SpecifySeer: target);
            Logger.Info($"{Player.GetNameWithRole()} 吐出了 {target.GetRealName()}", "Pelican.OnPlayerDeath");
        }
        EatenPlayers = new();
        SendRPC();
    }
    public override void OnSecondsUpdate(PlayerControl player, long now)
    {
        if (!AmongUsClient.Instance.AmHost || !Is(player)) return;

        MyLastPos = player.GetTruePosition();

        if (!GameStates.IsInTask)
        {
            if (EatenPlayers.Count >= 1)
            {
                EatenPlayers = new();
                SendRPC();
            }
            return;
        }

        foreach (var id in EatenPlayers)
        {
            var target = Utils.GetPlayerById(id);
            if (target == null) continue;
            var pos = Utils.GetBlackRoomPS();
            var dis = Vector2.Distance(pos, target.GetTruePosition());
            if (dis < 1f) continue;
            Utils.TP(target.NetTransform, pos);
            Utils.NotifyRoles(SpecifySeer: target);
        }
    }
    public bool OverrideKillButtonSprite(out string buttonName)
    {
        buttonName = "Vulture";
        return true;
    }
    public bool OverrideKillButtonText(out string text)
    {
        text = Translator.GetString("PelicanButtonText");
        return true;
    }
}