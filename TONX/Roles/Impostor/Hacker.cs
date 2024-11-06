using AmongUs.GameOptions;
using Hazel;
using System.Collections.Generic;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using UnityEngine;
using static TONX.Translator;

namespace TONX.Roles.Impostor;
public sealed class Hacker : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Hacker),
            player => new Hacker(player),
            CustomRoles.Hacker,
            () => RoleTypes.Shapeshifter,
            CustomRoleTypes.Impostor,
            1700,
            SetupOptionItem,
            "ha|駭客|黑客"
        );
    public Hacker(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        DeadBodyList = new();
    }

    static OptionItem OptionHackLimit;
    static OptionItem OptionKillCooldown;
    enum OptionName
    {
        HackLimit,
    }

    private int HackLimit = new();
    private static List<byte> DeadBodyList = new();
    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(2.5f, 180f, 2.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionHackLimit = IntegerOptionItem.Create(RoleInfo, 11, OptionName.HackLimit, new(1, 99, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);
    }
    public override void Add()
    {
        HackLimit = OptionHackLimit.GetInt();
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(HackLimit);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        HackLimit = reader.ReadInt32();
    }
    public float CalculateKillCooldown() => OptionKillCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ShapeshifterCooldown = 1f;
        AURoleOptions.ShapeshifterDuration = 1f;
    }
    public override string GetProgressText(bool comms = false) => Utils.ColorString(HackLimit >= 1 ? Color.red : Color.gray, $"({HackLimit})");
    public override int OverrideAbilityButtonUsesRemaining() => HackLimit;
    public override bool GetAbilityButtonText(out string text)
    {
        text = GetString("HackerShapeshiftText");
        return HackLimit >= 1;
    }
    public override bool GetAbilityButtonSprite(out string buttonName)
    {
        buttonName = "Hack";
        return true;
    }
    public override void OnStartMeeting() => DeadBodyList = new();
    public override void OnPlayerDeath(PlayerControl player, CustomDeathReason deathReason, bool isOnMeeting = false)
    {
        if (!isOnMeeting && player != null && !DeadBodyList.Contains(player.PlayerId))
            DeadBodyList.Add(player.PlayerId);
    }
    private bool Shapeshifting;
    public override void OnShapeshift(PlayerControl target)
    {
        Shapeshifting = !Is(target);

        if (!AmongUsClient.Instance.AmHost) return;

        if (!Shapeshifting || HackLimit < 1 || target == null || target.Is(CustomRoles.LazyGuy)) return;

        HackLimit--;
        SendRPC();

        var targetId = byte.MaxValue;

        // 寻找骇客击杀的尸体
        foreach (var db in DeadBodyList)
        {
            var dp = Utils.GetPlayerById(db);
            if (dp == null || dp.GetRealKiller() == null) continue;
            if (dp.GetRealKiller().PlayerId == Player.PlayerId) targetId = db;
        }

        // 未找到骇客击杀的尸体，寻找其他尸体
        if (targetId == byte.MaxValue && DeadBodyList.Count >= 1)
            targetId = DeadBodyList[IRandom.Instance.Next(0, DeadBodyList.Count)];

        if (targetId == byte.MaxValue)
            _ = new LateTask(() => target?.NoCheckStartMeeting(target?.Data), 0.15f, "Hacker Hacking Report Self");
        else
            _ = new LateTask(() => target?.NoCheckStartMeeting(Utils.GetPlayerById(targetId)?.Data), 0.15f, "Hacker Hacking Report");
    }
}