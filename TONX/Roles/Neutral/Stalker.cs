using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using InnerNet;

using static TONX.Options;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;

namespace TONX.Roles.Neutral
{
    public sealed class Stalker : RoleBase, IOverrideWinner
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Stalker),
                player => new Stalker(player),
                CustomRoles.Stalker,
                () => RoleTypes.Impostor,
                CustomRoleTypes.Neutral,
                75_1_2_1100,
                SetupOptionItem,
                "DarkHide",
                "#483d8b",
                true,
                countType: CountTypes.Crew
            );
        public Stalker(PlayerControl player)
        : base(
            RoleInfo,
            player,
            () => HasTask.False
        )
        {
            KillCooldown = OptionKillCooldown.GetFloat();
            HasImpostorVision = OptionHasImpostorVision.GetBool();
            CanCountNeutralKiller = OptionCanCountNeutralKiller.GetBool();

            IsWinKill = false;
        }
        public bool IsNK { get; private set; } = true;
        private static OptionItem OptionKillCooldown;
        private static OptionItem OptionHasImpostorVision;
        public static OptionItem OptionCanCountNeutralKiller;
        enum OptionName
        {
            StalkerCanCountNeutralKiller,
        }
        private static float KillCooldown;
        private static bool HasImpostorVision;
        public static bool CanCountNeutralKiller;

        public bool IsWinKill = false;

        public SchrodingerCat.TeamType SchrodingerCatChangeTo => SchrodingerCat.TeamType.Stalker;

        public bool CanUseSabotageButton() => false;
        private static void SetupOptionItem()
        {
            OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(2.5f, 180f, 2.5f), 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.ImpostorVision, false, false);
            OptionCanCountNeutralKiller = BooleanOptionItem.Create(RoleInfo, 12, OptionName.StalkerCanCountNeutralKiller, false, false);
        }

        public void OnMurderPlayerAsKiller(MurderInfo info)
        {
            if (!info.IsSuicide)
            {
                (var killer, var target) = info.AttemptTuple;

                var targetRole = target.GetCustomRole();
                if (!IsWinKill) IsWinKill = targetRole.IsImpostor();
                if (CanCountNeutralKiller && target.IsNeutralKiller()) IsWinKill = true;

                foreach (var pc in Main.AllPlayerControls)
                {
                    if (pc.Data.Disconnected) continue;
                    MessageWriter SabotageFixWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, (byte)RpcCalls.UpdateSystem, SendOption.Reliable, pc.GetClientId());
                    SabotageFixWriter.Write((byte)SystemTypes.Electrical);
                    MessageExtensions.WriteNetObject(SabotageFixWriter, pc);
                    AmongUsClient.Instance.FinishRpcImmediately(SabotageFixWriter);
                }
            }
        }
        public void CheckWin(ref CustomWinner WinnerTeam, ref HashSet<byte> WinnerIds)
        {
            if (Player.IsAlive() && WinnerTeam != CustomWinner.Stalker && IsWinKill)
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Stalker);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Stalker);
            }
             
        }
        public float CalculateKillCooldown() => KillCooldown;
        public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision);
        public bool CanUseImpostorVentButton() => false;
        public void ApplySchrodingerCatOptions(IGameOptions option) => ApplyGameOptions(option);
    }
}