using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using TONX.Modules;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using UnityEngine;

namespace TONX.Roles.Impostor;
public sealed class BallLightning : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(BallLightning),
            player => new BallLightning(player),
            CustomRoles.BallLightning,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            4400,
            SetupOptionItem,
            "li|球狀閃電|球闪|球状",
            experimental: true
        );
    public BallLightning(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Ghosts = new();

        CustomRoleManager.OnCheckMurderPlayerOthers_Before.Add(OnCheckMurderPlayerOthers_Before);
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    static OptionItem OptionKillCooldown;
    static OptionItem OptionConvertTime;
    static OptionItem OptionKillerConvertGhost;
    enum OptionName
    {
        BallLightningKillCooldown,
        BallLightningConvertTime,
        BallLightningKillerConvertGhost,
    }

    private static Dictionary<byte, byte> Ghosts;
    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.BallLightningKillCooldown, new(2.5f, 180f, 2.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionConvertTime = FloatOptionItem.Create(RoleInfo, 11, OptionName.BallLightningConvertTime, new(2.5f, 180f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionKillerConvertGhost = BooleanOptionItem.Create(RoleInfo, 12, OptionName.BallLightningKillerConvertGhost, true, false);
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(Ghosts.Count);
        Ghosts.Do(x => { sender.Writer.Write(x.Key); sender.Writer.Write(x.Value); });
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        Ghosts = new();
        for (int i = 0; i < reader.ReadInt32(); i++)
            Ghosts.Add(reader.ReadByte(), reader.ReadByte());
    }
    public float CalculateKillCooldown() => OptionKillCooldown.GetFloat();
    public static bool IsGhost(byte id) => Ghosts.ContainsKey(id);
    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {
        seen ??= seer;
        string mark = Utils.ColorString(Utils.GetRoleColor(CustomRoles.BallLightning), "■");
        return IsGhost(seen.PlayerId) ? mark : "";
    }
    private static bool OnCheckMurderPlayerOthers_Before(MurderInfo info)
    {
        if (info.IsSuicide) return true;
        if (!IsGhost(info.AttemptTarget.PlayerId)) return true;
        Logger.Info($"{info.AttemptKiller.GetNameWithRole()} 尝试交互的目标 {info.AttemptTarget.GetNameWithRole()} 是量子幽灵，操作被取消", "BallLightning.OnCheckMurderPlayerOthers_Before");
        return false;
    }
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        killer.SetKillCooldownV2();
        killer.RPCPlayCustomSound("Shield");
        StartConvertCountDown(killer, target);
        return false;
    }
    private static void StartConvertCountDown(PlayerControl killer, PlayerControl target)
    {
        new LateTask(() =>
        {
            if (GameStates.IsInGame && GameStates.IsInTask && target.IsAlive() && !target.IsEaten())
            {
                Ghosts.TryAdd(target.PlayerId, killer.PlayerId);
                (killer.GetRoleClass() as BallLightning)?.SendRPC();
                if (!killer.inVent) killer.RpcProtectedMurderPlayer(killer);
                Utils.NotifyRoles();
                Logger.Info($"{target.GetNameWithRole()} 转化为量子幽灵", "BallLightning.StartConvertCountDown");
            }
        }, OptionConvertTime.GetFloat(), "BallLightning.StartConvertCountDown");
    }
    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (!OptionKillerConvertGhost.GetBool() || IsGhost(killer.PlayerId)) return;
        StartConvertCountDown(target, killer);
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        List<byte> deList = new();
        foreach (var ghost in Ghosts)
        {
            var gs = Utils.GetPlayerById(ghost.Key);
            var killer = Utils.GetPlayerById(ghost.Value);
            if (killer == null || gs == null || !gs.IsAlive() || gs.Data.Disconnected)
            {
                deList.Add(gs.PlayerId);
                continue;
            }
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.PlayerId != gs.PlayerId && x.IsAlive() && !x.Is(CustomRoles.BallLightning) && !IsGhost(x.PlayerId) && !x.IsEaten()))
            {
                var pos = gs.transform.position;
                var dis = Vector2.Distance(pos, pc.transform.position);
                if (dis > 0.3f) continue;

                deList.Add(gs.PlayerId);
                var state = PlayerState.GetByPlayerId(gs.PlayerId);
                gs.SetDeathReason(CustomDeathReason.Quantization);
                gs.SetRealKiller(killer);
                CustomRoleManager.OnCheckMurder(
                    killer, gs,
                    gs, gs
                    );

                Logger.Info($"{gs.GetNameWithRole()} 作为量子幽灵因碰撞而死", "BallLightning.OnFixedUpdate");
                break;
            }
        }
        deList.Do(id => Ghosts.Remove(id));
        if (deList.Count > 0)
        {
            SendRPC();
            Utils.NotifyRoles();
        }
    }
    public override void OnStartMeeting()
    {
        foreach (var ghost in Ghosts)
        {
            var player = Utils.GetPlayerById(ghost.Key);
            if (player == null) continue;
            MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.Quantization, player.PlayerId);
            player.SetRealKiller(ghost.Value);
            Logger.Info($"{player.GetNameWithRole()} 作为量子幽灵参与会议，将在会议后死亡", "BallLightning.OnStartMeeting");
        }
        Ghosts = new();
        SendRPC();
    }
    public bool OverrideKillButtonText(out string text)
    {
        text = Translator.GetString("BallLightningButtonText");
        return true;
    }
}
