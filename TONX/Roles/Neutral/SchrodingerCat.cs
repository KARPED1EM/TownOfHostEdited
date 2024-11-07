using AmongUs.GameOptions;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using TONX.Modules;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using UnityEngine;

namespace TONX.Roles.Neutral;

// マッドが属性化したらマッド状態時の特別扱いを削除する
public sealed class SchrodingerCat : RoleBase, IAdditionalWinner, IDeathReasonSeeable, IKillFlashSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SchrodingerCat),
            player => new SchrodingerCat(player),
            CustomRoles.SchrodingerCat,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            50400,
            SetupOptionItem,
            "sc|cat|猫|猫猫|薛丁格的貓|貓",
            "#696969",
            introSound: () => GetIntroSound(RoleTypes.Impostor)
        );
    public SchrodingerCat(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        CanWinTheCrewmateBeforeChange = OptionCanWinTheCrewmateBeforeChange.GetBool();
        ChangeTeamWhenExile = OptionChangeTeamWhenExile.GetBool();
        CanSeeKillableTeammate = OptionCanSeeKillableTeammate.GetBool();
    }
    static OptionItem OptionCanWinTheCrewmateBeforeChange;
    static OptionItem OptionChangeTeamWhenExile;
    static OptionItem OptionCanSeeKillableTeammate;

    enum OptionName
    {
        CanBeforeSchrodingerCatWinTheCrewmate,
        SchrodingerCatExiledTeamChanges,
        SchrodingerCatCanSeeKillableTeammate,
    }
    static bool CanWinTheCrewmateBeforeChange;
    static bool ChangeTeamWhenExile;
    static bool CanSeeKillableTeammate;

    /// <summary>
    /// 自分をキルしてきた人のロール
    /// </summary>
    private ISchrodingerCatOwner owner = null;
    private TeamType _team = TeamType.None;
    /// <summary>
    /// 現在の所属陣営<br/>
    /// 変更する際は特段の事情がない限り<see cref="RpcSetTeam"/>を使ってください
    /// </summary>
    public TeamType Team
    {
        get => _team;
        private set
        {
            logger.Info($"{Player.GetRealName()}の陣営を{value}に変更");
            _team = value;
        }
    }
    public bool AmMadmate => Team == TeamType.Mad;
    public Color DisplayRoleColor => GetCatColor(Team);
    private static LogHandler logger = Logger.Handler(nameof(SchrodingerCat));

    public static void SetupOptionItem()
    {
        OptionCanWinTheCrewmateBeforeChange = BooleanOptionItem.Create(RoleInfo, 10, OptionName.CanBeforeSchrodingerCatWinTheCrewmate, true, false);
        OptionChangeTeamWhenExile = BooleanOptionItem.Create(RoleInfo, 11, OptionName.SchrodingerCatExiledTeamChanges, false, false);
        OptionCanSeeKillableTeammate = BooleanOptionItem.Create(RoleInfo, 12, OptionName.SchrodingerCatCanSeeKillableTeammate, false, false);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        owner?.ApplySchrodingerCatOptions(opt);
    }
    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        var killer = info.AttemptKiller;

        //自殺ならスルー
        if (info.IsSuicide) return true;

        if (Team == TeamType.None)
        {
            info.CanKill = false;
            ChangeTeamOnKill(killer);
            return false;
        }
        return true;
    }
    /// <summary>
    /// キルしてきた人に応じて陣営の状態を変える
    /// </summary>
    private void ChangeTeamOnKill(PlayerControl killer)
    {
        killer.RpcProtectedMurderPlayer(Player);
        if (killer.GetRoleClass() is ISchrodingerCatOwner catOwner)
        {
            catOwner.OnSchrodingerCatKill(this);
            RpcSetTeam(catOwner.SchrodingerCatChangeTo);
            owner = catOwner;
        }
        else
        {
            logger.Warn($"未知のキル役職からのキル: {killer.GetNameWithRole()}");
        }

        RevealNameColors(killer);

        Utils.NotifyRoles();
        Utils.MarkEveryoneDirtySettings();
    }
    /// <summary>
    /// キルしてきた人とオプションに応じて名前の色を開示する
    /// </summary>
    private void RevealNameColors(PlayerControl killer)
    {
        if (CanSeeKillableTeammate)
        {
            var killerRoleId = killer.GetCustomRole();
            var killerTeam = Main.AllPlayerControls.Where(player => (AmMadmate && player.Is(CustomRoleTypes.Impostor)) || player.Is(killerRoleId));
            foreach (var member in killerTeam)
            {
                NameColorManager.Add(member.PlayerId, Player.PlayerId, RoleInfo.RoleColorCode);
                NameColorManager.Add(Player.PlayerId, member.PlayerId);
            }
        }
        else
        {
            NameColorManager.Add(killer.PlayerId, Player.PlayerId, RoleInfo.RoleColorCode);
            NameColorManager.Add(Player.PlayerId, killer.PlayerId);
        }
    }
    public override void OverrideTrueRoleName(ref Color roleColor, ref string roleText)
    {
        // 陣営変化前なら上書き不要
        if (Team == TeamType.None)
        {
            return;
        }
        roleColor = DisplayRoleColor;
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (exiled.PlayerId != Player.PlayerId || Team != TeamType.None || !ChangeTeamWhenExile)
        {
            return;
        }
        ChangeTeamRandomly();
    }
    /// <summary>
    /// ゲームに存在している陣営の中からランダムに自分の陣営を変更する
    /// </summary>
    private void ChangeTeamRandomly()
    {
        var rand = IRandom.Instance;
        List<TeamType> candidates = new(4)
        {
            TeamType.Crew,
            TeamType.Mad,
        };
        if (CustomRoles.Jackal.IsExist())
        {
            candidates.Add(TeamType.Jackal);
        }
        if (CustomRoles.Pelican.IsExist())
        {
            candidates.Add(TeamType.Pelican);
        }
        if (CustomRoles.BloodKnight.IsExist())
        {
            candidates.Add(TeamType.BloodKnight);
        }
        if (CustomRoles.Demon.IsExist())
        {
            candidates.Add(TeamType.Demon);
        }
        if (CustomRoles.Hater.IsExist())
        {
            candidates.Add(TeamType.Hater);
        }
        if (CustomRoles.Stalker.IsExist())
        {
            candidates.Add(TeamType.Stalker);
        }
        var team = candidates[rand.Next(candidates.Count)];
        RpcSetTeam(team);
    }
    public bool CheckWin(ref CustomRoles winnerRole)
    {
        bool? won = Team switch
        {
            TeamType.None => CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate && CanWinTheCrewmateBeforeChange,
            TeamType.Mad => CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor,
            TeamType.Crew => CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate,
            TeamType.Jackal => CustomWinnerHolder.WinnerTeam == CustomWinner.Jackal,
            TeamType.Pelican => CustomWinnerHolder.WinnerTeam == CustomWinner.Pelican,
            TeamType.BloodKnight => CustomWinnerHolder.WinnerTeam == CustomWinner.BloodKnight,
            TeamType.Demon => CustomWinnerHolder.WinnerTeam == CustomWinner.Demon,
            TeamType.Hater => CustomWinnerHolder.WinnerRoles.Contains(CustomRoles.Hater),
            TeamType.Stalker => CustomWinnerHolder.WinnerRoles.Contains(CustomRoles.Stalker),

            _ => null,
        };
        if (!won.HasValue)
        {
            logger.Warn($"不明な猫の勝利チェック: {Team}");
            return false;
        }
        return won.Value;
    }
    public void RpcSetTeam(TeamType team)
    {
        Team = team;
        if (AmongUsClient.Instance.AmHost)
        {
            using var sender = CreateSender();
            sender.Writer.Write((byte)team);
        }
    }
    public override void ReceiveRPC(MessageReader reader)
    {

        Team = (TeamType)reader.ReadByte();
    }

    /// <summary>
    /// 陣営状態
    /// </summary>
    public enum TeamType : byte
    {
        None = 0,

        // 10-49 不显示在警长的猫可击杀目标选项下的
        Mad = 10,
        Crew,

        // 50- 与上相反
        Jackal = 50,
        Pelican,
        BloodKnight,
        Demon,
        Hater,
        Stalker

    }
    public static Color GetCatColor(TeamType catType)
    {
        Color? color = catType switch
        {
            TeamType.None => RoleInfo.RoleColor,
            TeamType.Mad => Utils.GetRoleColor(CustomRoles.Madmate),
            TeamType.Crew => Utils.GetRoleColor(CustomRoles.Crewmate),
            TeamType.Jackal => Utils.GetRoleColor(CustomRoles.Jackal),
            TeamType.Pelican => Utils.GetRoleColor(CustomRoles.Pelican),
            TeamType.BloodKnight => Utils.GetRoleColor(CustomRoles.BloodKnight),
            TeamType.Demon => Utils.GetRoleColor(CustomRoles.Demon),
            TeamType.Hater => Utils.GetRoleColor(CustomRoles.Hater),
            TeamType.Stalker => Utils.GetRoleColor(CustomRoles.Stalker),

            _ => null,
        };
        if (!color.HasValue)
        {
            logger.Warn($"不明な猫に対する色の取得: {catType}");
            return Utils.GetRoleColor(CustomRoles.Crewmate);
        }
        return color.Value;
    }
}
