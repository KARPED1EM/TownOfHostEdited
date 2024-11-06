using AmongUs.GameOptions;

using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;

// 来源：https://github.com/Yumenopai/TownOfHost_Y
namespace TONX.Roles.Impostor;
public sealed class Greedy : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Greedy),
            player => new Greedy(player),
            CustomRoles.Greedy,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            3400,
            SetupOptionItem,
            "gr|貪婪者|贪婪"
        );
    public Greedy(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    static OptionItem OptionOddKillCooldown;
    static OptionItem OptionEvenKillCooldown;
    enum OptionName
    {
        OddKillCooldown,
        EvenKillCooldown,
    }

    private int KillCount;
    private bool IsOdd => KillCount % 2 == 0;
    private static void SetupOptionItem()
    {
        OptionOddKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.OddKillCooldown, new(2.5f, 180f, 2.5f), 25f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionEvenKillCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.EvenKillCooldown, new(2.5f, 180f, 2.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public override void Add() => KillCount = 0;
    public float CalculateKillCooldown() => IsOdd ? OptionOddKillCooldown.GetFloat() : OptionEvenKillCooldown.GetFloat();
    public override void OnStartMeeting() => KillCount = 0;
    public void BeforeMurderPlayerAsKiller(MurderInfo info)
    {
        KillCount++;
        Player.ResetKillCooldown();
        Player.SyncSettings();
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
        => Player.RpcResetAbilityCooldown();
}