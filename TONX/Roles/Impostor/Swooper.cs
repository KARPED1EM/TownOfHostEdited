using AmongUs.GameOptions;
using Hazel;
using System.Text;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using static TONX.Translator;

namespace TONX.Roles.Impostor;
public sealed class Swooper : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Swooper),
            player => new Swooper(player),
            CustomRoles.Swooper,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            3900,
            SetupOptionItem,
            "sw|隱匿者|隐匿|隐身人|隐身"
        );

    public Swooper(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        InvisTime = -1;
        LastTime = -1;
        VentedId = -1;
    }

    static OptionItem SwooperCooldown;
    static OptionItem SwooperDuration;
    enum OptionName
    {
        SwooperCooldown,
        SwooperDuration,
    }

    private long InvisTime;
    private long LastTime;
    private int VentedId;
    private static void SetupOptionItem()
    {
        SwooperCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.SwooperCooldown, new(2.5f, 180f, 2.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        SwooperDuration = FloatOptionItem.Create(RoleInfo, 11, OptionName.SwooperDuration, new(2.5f, 180f, 2.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(InvisTime.ToString());
        sender.Writer.Write(LastTime.ToString());
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        InvisTime = long.Parse(reader.ReadString());
        LastTime = long.Parse(reader.ReadString());
    }
    public bool CanGoInvis() => GameStates.IsInTask && InvisTime == -1 && LastTime == -1;
    public bool IsInvis() => InvisTime != -1;
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || LastTime == -1) return;
        var now = Utils.GetTimeStamp();

        if (LastTime + (long)SwooperCooldown.GetFloat() < now)
        {
            LastTime = -1;
            if (!player.IsModClient()) player.Notify(GetString("SwooperCanVent"));
            SendRPC();
        }
    }
    public override void OnSecondsUpdate(PlayerControl player, long now)
    {
        if (!AmongUsClient.Instance.AmHost || !IsInvis()) return;
        var remainTime = InvisTime + (long)SwooperDuration.GetFloat() - now;
        if (remainTime < 0)
        {
            LastTime = now;
            InvisTime = -1;
            SendRPC();
            player?.MyPhysics?.RpcBootFromVent(VentedId != -1 ? VentedId : Main.LastEnteredVent[player.PlayerId].Id);
            NameNotifyManager.Notify(player, GetString("SwooperInvisStateOut"));
            return;
        }
        else if (remainTime <= 10)
        {
            if (!player.IsModClient()) player.Notify(string.Format(GetString("SwooperInvisStateCountdown"), remainTime));
        }
    }
    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        var now = Utils.GetTimeStamp();
        if (IsInvis())
        {
            LastTime = now;
            InvisTime = -1;
            SendRPC();
            NameNotifyManager.Notify(Player, GetString("SwooperInvisStateOut"));
            return false;
        }
        else
        {
            new LateTask(() =>
            {
                if (CanGoInvis())
                {
                    VentedId = ventId;

                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(physics.NetId, 34, SendOption.Reliable, Player.GetClientId());
                    writer.WritePacked(ventId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);

                    InvisTime = now;
                    SendRPC();

                    NameNotifyManager.Notify(Player, GetString("SwooperInvisState"), SwooperDuration.GetFloat());
                }
                else
                {
                    physics.RpcBootFromVent(ventId);
                    NameNotifyManager.Notify(Player, GetString("SwooperInvisInCooldown"));
                }
            }, 0.5f, "Swooper Vent");
            return true;
        }
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        if (!isForHud || isForMeeting) return "";

        var str = new StringBuilder();
        if (IsInvis())
        {
            var remainTime = InvisTime + (long)SwooperDuration.GetFloat() - Utils.GetTimeStamp();
            str.Append(string.Format(GetString("SwooperInvisStateCountdown"), remainTime));
        }
        else if (LastTime != -1)
        {
            var cooldown = LastTime + (long)SwooperCooldown.GetFloat() - Utils.GetTimeStamp();
            str.Append(string.Format(GetString("SwooperInvisCooldownRemain"), cooldown));
        }
        else
        {
            str.Append(GetString("SwooperCanVent"));
        }
        return str.ToString();
    }
    public void BeforeMurderPlayerAsKiller(MurderInfo info)
    {
        if (!IsInvis()) return;
        var (killer, target) = info.AttemptTuple;

        Utils.TP(killer.NetTransform, target.GetTruePosition());
        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        killer.SetKillCooldownV2();

        target.SetRealKiller(killer);
        target.RpcMurderPlayerV2(target);

        info.DoKill = false;
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        LastTime = -1;
        InvisTime = -1;
        SendRPC();
    }
    public override void OnGameStart()
    {
        LastTime = Utils.GetTimeStamp();
        SendRPC();
    }
}