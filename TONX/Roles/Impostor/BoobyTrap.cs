using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;

namespace TONX.Roles.Impostor;
public sealed class BoobyTrap : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(BoobyTrap),
            player => new BoobyTrap(player),
            CustomRoles.BoobyTrap,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            4200,
            SetupOptionItem,
            "bt|詭雷"
        );
    public BoobyTrap(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Boobytraps = new();
    }

    static OptionItem OptionSuicideDelay;
    enum OptionName
    {
        BoobyTrapSuicideDelay,
    }

    private static Dictionary<byte, byte> Boobytraps;
    private static void SetupOptionItem()
    {
        OptionSuicideDelay = FloatOptionItem.Create(RoleInfo, 10, OptionName.BoobyTrapSuicideDelay, new(0f, 5f, 1f), 1f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public bool OverrideKillButtonText(out string text)
    {
        text = Translator.GetString("BoobyTrapKillButtonText");
        return true;
    }
    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        if (info.IsSuicide) return;
        var (killer, target) = info.AttemptTuple;
        Boobytraps.TryAdd(target.PlayerId, killer.PlayerId);

        new LateTask(() =>
        {
            if (Main.AllAlivePlayerControls.Count() > 1 && killer.IsAlive() && !GameStates.IsEnded)
            {
                killer.SetDeathReason(CustomDeathReason.Misfire);
                killer.RpcMurderPlayerV2(killer);
            }
        }, OptionSuicideDelay.GetFloat(), "BoobyTrap Death Delay");
    }
    public override bool OnCheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (Boobytraps.ContainsKey(target.PlayerId) && reporter.IsAlive())
        {
            var killerId = Boobytraps[target.PlayerId];
            reporter.SetDeathReason(CustomDeathReason.Bombed);
            reporter.SetRealKiller(killerId);

            reporter.RpcMurderPlayerV2(reporter);
            RPC.PlaySoundRPC(killerId, Sounds.KillSound);

            Boobytraps.TryAdd(reporter.PlayerId, killerId);

            return false;
        }
        return true;
    }

}