using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using TONX.Modules;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using UnityEngine;
using static TONX.Translator;

namespace TONX.Roles.Impostor;
public sealed class Vampire : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Vampire),
            player => new Vampire(player),
            CustomRoles.Vampire,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            1400,
            SetupOptionItem,
            "va|吸血",
            introSound: () => GetIntroSound(RoleTypes.Shapeshifter)
        );
    public Vampire(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        KillDelay = OptionKillDelay.GetFloat();

        BittenPlayers.Clear();
    }

    static OptionItem OptionKillDelay;
    enum OptionName
    {
        VampireKillDelay
    }

    static float KillDelay;

    public bool CanBeLastImpostor { get; } = false;
    Dictionary<byte, float> BittenPlayers = new(14);

    private static void SetupOptionItem()
    {
        OptionKillDelay = FloatOptionItem.Create(RoleInfo, 10, OptionName.VampireKillDelay, new(1f, 1000f, 1f), 7f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;

        if (target.Is(CustomRoles.Bait)) return true;
        if (info.IsFakeSuicide) return true;

        //誰かに噛まれていなければ登録
        if (!BittenPlayers.ContainsKey(target.PlayerId))
        {
            killer.SetKillCooldownV2();
            killer.RPCPlayCustomSound("Bite");
            BittenPlayers.Add(target.PlayerId, 0f);
        }
        return false;
    }
    public override void OnFixedUpdate(PlayerControl _)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask) return;

        foreach (var (targetId, timer) in BittenPlayers.ToArray())
        {
            if (timer >= KillDelay)
            {
                var target = Utils.GetPlayerById(targetId);
                KillBitten(target);
                BittenPlayers.Remove(targetId);
            }
            else
            {
                BittenPlayers[targetId] += Time.fixedDeltaTime;
            }
        }
    }
    public override void OnReportDeadBody(PlayerControl _, NetworkedPlayerInfo __)
    {
        foreach (var targetId in BittenPlayers.Keys)
        {
            var target = Utils.GetPlayerById(targetId);
            KillBitten(target, true);
        }
        BittenPlayers.Clear();
    }
    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("VampireBiteButtonText");
        return true;
    }
    public bool OverrideKillButtonSprite(out string buttonName)
    {
        buttonName = "Bite";
        return true;
    }

    private void KillBitten(PlayerControl target, bool isButton = false)
    {
        var vampire = Player;
        if (target.IsAlive())
        {
            PlayerState.GetByPlayerId(target.PlayerId).DeathReason = CustomDeathReason.Bite;
            target.SetRealKiller(vampire);
            CustomRoleManager.OnCheckMurder(
                vampire, target,
                target, target
            );
            Logger.Info($"Vampireに噛まれている{target.name}を自爆させました。", "Vampire.KillBitten");
            if (!isButton && vampire.IsAlive())
            {
                RPC.PlaySoundRPC(vampire.PlayerId, Sounds.KillSound);
            }
        }
        else
        {
            Logger.Info($"Vampireに噛まれている{target.name}はすでに死んでいました。", "Vampire.KillBitten");
        }
    }
}
