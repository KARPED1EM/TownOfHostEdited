using AmongUs.GameOptions;
using Hazel;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using static TONX.Translator;

namespace TONX.Roles.Neutral;
public sealed class BloodKnight : RoleBase, IKiller, ISchrodingerCatOwner
{
    public static readonly SimpleRoleInfo RoleInfo =
       SimpleRoleInfo.Create(
           typeof(BloodKnight),
           player => new BloodKnight(player),
           CustomRoles.BloodKnight,
           () => RoleTypes.Impostor,
           CustomRoleTypes.Neutral,
           50923,
           SetupOptionItem,
           "bn|ÊÈÑªòTÊ¿|ÑªÆï|ÆïÊ¿",
           "#630000",
           true,
           countType: CountTypes.BloodKnight
       );
    public BloodKnight(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        CanVent = OptionCanVent.GetBool();
    }

    static OptionItem OptionKillCooldown;
    static OptionItem OptionCanVent;
    static OptionItem OptionHasImpostorVision;
    static OptionItem OptionProtectDuration;
    enum OptionName
    {
        BKProtectDuration
    }
    public static bool CanVent;

    private long ProtectStartTime;

    public SchrodingerCat.TeamType SchrodingerCatChangeTo => SchrodingerCat.TeamType.BloodKnight;

    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(2.5f, 180f, 2.5f), 25f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, true, false);
        OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 13, GeneralOption.ImpostorVision, true, false);
        OptionProtectDuration = FloatOptionItem.Create(RoleInfo, 14, OptionName.BKProtectDuration, new(1f, 999f, 1f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public override void Add() => ProtectStartTime = 0;
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(ProtectStartTime.ToString());
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        ProtectStartTime = long.Parse(reader.ReadString());
    }
    public float CalculateKillCooldown() => OptionKillCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(OptionHasImpostorVision.GetBool());
    public static void SetHudActive(HudManager __instance, bool _) => __instance.SabotageButton.ToggleVisible(false);
    public bool CanUseSabotageButton() => false;
    private bool InProtect() => ProtectStartTime != 0 && ProtectStartTime + OptionProtectDuration.GetFloat() > Utils.GetTimeStamp();
    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        if (info.IsSuicide) return;
        ProtectStartTime = Utils.GetTimeStamp();
        SendRPC();
        Utils.NotifyRoles(Player);
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (ProtectStartTime != 0 && ProtectStartTime + OptionProtectDuration.GetFloat() < Utils.GetTimeStamp())
        {
            ProtectStartTime = 0;
            player.Notify(GetString("BKProtectOut"));
        }
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        if (!Is(seer) || seen != null || isForMeeting) return "";
        if (!InProtect())
        {
            return isForHud ? GetString("BKSkillNotice") : "";
        }
        else return isForHud
            ? string.Format(GetString("BKSkillTimeRemain"), ProtectStartTime + OptionProtectDuration.GetFloat() - Utils.GetTimeStamp())
            : GetString("BKInProtectForUnModed");
    }
}