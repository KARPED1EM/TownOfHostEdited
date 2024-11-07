using AmongUs.GameOptions;
using Hazel;
using MS.Internal.Xml.XPath;
using System.Collections.Generic;
using System.Linq;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using UnityEngine;
using static TONX.Translator;
using static UnityEngine.GraphicsBuffer;

namespace TONX.Roles.Neutral;
public sealed class Revolutionist : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Revolutionist),
            player => new Revolutionist(player),
            CustomRoles.Revolutionist,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            75_1_3_0100,
            SetupOptionItem,
            "re|革命|改个",
            "#ba4d06",
            isDesyncImpostor: true,
            introSound: () => GetIntroSound(RoleTypes.Crewmate)
        );
    public Revolutionist(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        DouseTime = RevolutionistDrawTime.GetFloat();
        DouseCooldown = RevolutionistCooldown.GetFloat();

        TargetInfo = null;
    }
    private static OptionItem RevolutionistDrawTime;
    private static OptionItem RevolutionistCooldown;
    private static OptionItem RevolutionistDrawCount;
    private static OptionItem RevolutionistKillProbability;
    private static OptionItem RevolutionistVentCountDown;


    private static float DouseTime;
    private static float DouseCooldown;
    private TimerInfo TargetInfo;
    public static Dictionary<byte, bool> Isdraw = new();
    public static Dictionary<byte, long> RevolutionistStart = new();
    public static Dictionary<byte, long> RevolutionistLastTime = new();
    public static Dictionary<byte, int> RevolutionistCountdown = new();

    public class TimerInfo
    {
        public byte TargetId;
        public float Timer;
        public TimerInfo(byte targetId, float timer)
        {
            TargetId = targetId;
            Timer = timer;
        }
    }


    public bool IsKiller { get; private set; } = false;
    public bool CanKill { get; private set; } = false;

    enum OptionName
    {
        RevolutionistDrawTime,
        RevolutionistCooldown,
        RevolutionistDrawCount,
        RevolutionistKillProbability,
        RevolutionistVentCountDown,
    }
    private static void SetupOptionItem()
    {
        RevolutionistDrawTime = FloatOptionItem.Create(RoleInfo, 10, OptionName.RevolutionistDrawTime, new(0f, 10f, 1f), 3f, false)
           .SetValueFormat(OptionFormat.Seconds);
        RevolutionistCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.RevolutionistCooldown, new(5f, 100f, 1f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        RevolutionistDrawCount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.RevolutionistDrawCount, new(1, 14, 1), 6, false)
            .SetValueFormat(OptionFormat.Players);
        RevolutionistKillProbability = IntegerOptionItem.Create(RoleInfo, 13, OptionName.RevolutionistKillProbability, new(0, 100, 5), 15, false)
            .SetValueFormat(OptionFormat.Percent);
        RevolutionistVentCountDown = FloatOptionItem.Create(RoleInfo, 14, OptionName.RevolutionistVentCountDown, new(1f, 180f, 1f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public bool CanUseKillButton() => !IsDrawDone(Player);
    public bool CanUseImpostorVentButton() => IsDrawDone(Player) && !Player.inVent;
    public float CalculateKillCooldown() => DouseCooldown;
    public bool CanUseSabotageButton() => false;
    public override string GetProgressText(bool comms = false)
    {
        var draw = GetDrawPlayerCount(out var _);
        return Utils.ColorString(RoleInfo.RoleColor.ShadeColor(0.25f), $"({draw.Item1}/{draw.Item2})");
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }
    enum RPC_type
    {
        SetDrawPlayer,
        SetCurrentDrawTarget
    }
    private void SendRPC(RPC_type rpcType, byte targetId = byte.MaxValue, bool Isdraw = false)
    {
        using var sender = CreateSender();
        sender.Writer.Write(targetId);

        sender.Writer.Write((byte)rpcType);
        if (rpcType == RPC_type.SetDrawPlayer)
            sender.Writer.Write(Isdraw);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        var targetId = reader.ReadByte();
        var rpcType = (RPC_type)reader.ReadByte();
        switch (rpcType)
        {
            case RPC_type.SetDrawPlayer:
                bool draw = reader.ReadBoolean();
                Isdraw[targetId] = draw;
                break;
            case RPC_type.SetCurrentDrawTarget:
                TargetInfo = new(targetId, 0f);
                break;
        }
    }

    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;

        Logger.Info("Revolutionist start douse", "OnCheckMurderAsKiller");
        killer.SetKillCooldown(DouseTime);
        if (!Isdraw[target.PlayerId] && TargetInfo == null)
        {
            TargetInfo = new(target.PlayerId, 0f);
            Utils.NotifyRoles(SpecifySeer: killer);
            SendRPC(RPC_type.SetCurrentDrawTarget, target.PlayerId);
        }
        return false;
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        TargetInfo = null;
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsInTask && TargetInfo != null)//当革命家拉拢一个玩家时
        {
            if (!Player.IsAlive())
            {
                TargetInfo = null;
                Utils.NotifyRoles(SpecifySeer: Player);
                SendRPC(RPC_type.SetCurrentDrawTarget);
            }
            else
            {
                var rv_target = Utils.GetPlayerById(TargetInfo.TargetId);//塗られる人
                var rv_time = TargetInfo.Timer;//拉拢时间
                if (!rv_target.IsAlive())
                {
                    TargetInfo = null;
                }
                else if (rv_time >= RevolutionistDrawTime.GetFloat())//在一起时间超过多久
                {
                    player.SetKillCooldown();
                    TargetInfo = null;//拉拢完成从字典中删除
                    Isdraw[rv_target.PlayerId] = true;//完成拉拢
                    SendRPC(RPC_type.SetDrawPlayer, rv_target.PlayerId, true);
                    Utils.NotifyRoles(player);
                    SendRPC(RPC_type.SetCurrentDrawTarget);
                    if (IRandom.Instance.Next(1, 100) <= RevolutionistKillProbability.GetInt())
                    {
                        rv_target.SetRealKiller(player);
                        rv_target.SetDeathReason(CustomDeathReason.Sacrifice);
                        player.RpcMurderPlayerV2(rv_target);
                        Logger.Info($"Revolutionist: {player.GetNameWithRole()} killed {rv_target.GetNameWithRole()}", "Revolutionist");
                    }
                }
                else
                {
                    float dis;
                    dis = Vector2.Distance(Player.transform.position, rv_target.transform.position);//距離を出す
       
                    if (dis <= 1.75f)//在一定距离内则计算时间
                    {
                        TargetInfo.Timer += Time.fixedDeltaTime;
                    }
                    else//否则删除
                    {
                        TargetInfo = null;
                        Utils.NotifyRoles(SpecifySeer: Player);
                        SendRPC(RPC_type.SetCurrentDrawTarget);

                        Logger.Info($"Canceled: {Player.GetNameWithRole()}", "Revolutionist");
                    }
                }
            }
        }
        if (GameStates.IsInTask && IsDrawDone(Player) && player.IsAlive())
        {
            if (RevolutionistLastTime.ContainsKey(player.PlayerId))
            {
                long nowtime = Utils.GetTimeStamp();
                if (RevolutionistLastTime[player.PlayerId] != nowtime) RevolutionistLastTime[player.PlayerId] = nowtime;
                int time = (int)(RevolutionistLastTime[player.PlayerId] - RevolutionistStart[player.PlayerId]);
                int countdown = RevolutionistVentCountDown.GetInt() - time;
                RevolutionistCountdown.Clear();
                if (countdown <= 0)//倒计时结束
                {
                    GetDrawPlayerCount(out var y);
                    foreach (var pc in y.Where(x => x != null && x.IsAlive()))
                    {
                        pc.Data.IsDead = true;
                        pc.SetDeathReason(CustomDeathReason.Sacrifice);
                        pc.RpcMurderPlayerV2(pc);
                        pc.SetDeathReason(CustomDeathReason.Sacrifice);
                        Utils.NotifyRoles(pc);
                    }
                    player.Data.IsDead = true;
                    player.SetDeathReason(CustomDeathReason.Sacrifice);
                    player.RpcMurderPlayerV2(player);
                }
                else
                {
                    RevolutionistCountdown.Add(player.PlayerId, countdown);
                }
            }
            else
            {
                RevolutionistLastTime.TryAdd(player.PlayerId, RevolutionistStart[player.PlayerId]);
            }
        }
        else //如果不存在字典
        {
            RevolutionistStart.TryAdd(player.PlayerId, Utils.GetTimeStamp());
        }

    }

    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (GameStates.IsInGame && IsDrawDone(Player))
        {
            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (pc.PlayerId != Player.PlayerId)
                {
                    //生存者は焼殺
                    pc.SetRealKiller(Player);
                    pc.RpcMurderPlayer(pc);
                    var state = PlayerState.GetByPlayerId(pc.PlayerId);
                    state.DeathReason = CustomDeathReason.Torched;
                    state.SetDead();
                }
                else
                    RPC.PlaySoundRPC(pc.PlayerId, Sounds.KillSound);
            }
            CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Revolutionist); //焼殺で勝利した人も勝利させる
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
            return true;
        }
        return false;
    }
    
    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("RevolutionistDouseButtonText");
        return true;
    }
    public override bool GetAbilityButtonText(out string text)
    {
        text = GetString("RevolutionistVetnButtonText");
        return true;
    }
    public bool OverrideKillButtonSprite(out string buttonName)
    {
        buttonName = "Douse";
        return true;
    }
    public override bool GetAbilityButtonSprite(out string buttonName)
    {
        buttonName = "Ignite";
        return true;
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {
        //seenが省略の場合seer
        seen ??= seer;

        if (IsDrawPlayer(seen.PlayerId)) //seerがtargetに既にオイルを塗っている(完了)
            return Utils.ColorString(RoleInfo.RoleColor, "●");
        if (!isForMeeting && TargetInfo?.TargetId == seen.PlayerId) //オイルを塗っている対象がtarget
            return Utils.ColorString(RoleInfo.RoleColor, "○");

        return "";
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        if (isForMeeting) return "";
        //seenが省略の場合seer
        seen ??= seer;
        //seeおよびseenが自分である場合以外は関係なし
        if (!Is(seer) || !Is(seen)) return "";

        return IsDrawDone(Player) ? Utils.ColorString(RoleInfo.RoleColor, GetString("EnterVentToWin")) : "";
    }
    public bool IsDrawPlayer(byte targetId) => Isdraw.TryGetValue(targetId, out bool isdraw) && isdraw;
    public static bool IsDrawDone(PlayerControl player)
    {
        if (player.GetRoleClass() is not Revolutionist revolutionist) return false;
        var count = revolutionist.GetDrawPlayerCount(out var _);
        return count.Item1 == count.Item2;
    }
    public (int, int) GetDrawPlayerCount(out List<PlayerControl> winnerList)
    {
        int draw = 0;
        int all = RevolutionistDrawCount.GetInt();
        int max = Main.AllAlivePlayerControls.Count();
        if (Player.IsAlive()) max--;
        winnerList = new();
        if (all > max) all = max;
        foreach (var pc in Main.AllPlayerControls)
        {
            if (Isdraw.TryGetValue(pc.PlayerId, out var isDraw) && isDraw)
            {
                winnerList.Add(pc);
                draw++;
            }
        }
        return (draw, all);
    }
}
//*/