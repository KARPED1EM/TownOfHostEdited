using AmongUs.GameOptions;
using Hazel;

using TONX.Modules;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using static TONX.Translator;

namespace TONX.Roles.Impostor;
public sealed class Assassin : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Assassin),
            player => new Assassin(player),
            CustomRoles.Assassin,
            () => RoleTypes.Shapeshifter,
            CustomRoleTypes.Impostor,
            1600,
            SetupOptionItem,
            "as|忍者"
        );
    public Assassin(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    static OptionItem MarkCooldown;
    static OptionItem AssassinateCooldown;
    static OptionItem CanKillAfterAssassinate;
    enum OptionName
    {
        AssassinMarkCooldown,
        AssassinAssassinateCooldown,
        AssassinCanKillAfterAssassinate,
    }

    public byte MarkedPlayer = new();
    private static void SetupOptionItem()
    {
        MarkCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.AssassinMarkCooldown, new(2.5f, 180f, 2.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        AssassinateCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.AssassinAssassinateCooldown, new(2.5f, 180f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        CanKillAfterAssassinate = BooleanOptionItem.Create(RoleInfo, 12, OptionName.AssassinCanKillAfterAssassinate, true, false);
    }
    public override void Add()
    {
        MarkedPlayer = byte.MaxValue;
        Shapeshifting = false;
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(MarkedPlayer);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        MarkedPlayer = reader.ReadByte();
    }
    public bool CanUseKillButton()
    {
        if (!Player.IsAlive()) return false;
        if (!CanKillAfterAssassinate.GetBool() && Shapeshifting) return false;
        return true;
    }
    public float CalculateKillCooldown() => Shapeshifting ? Options.DefaultKillCooldown : MarkCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt) => AURoleOptions.ShapeshifterCooldown = AssassinateCooldown.GetFloat();
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        if (info.IsSuicide) return true;
        var (killer, target) = info.AttemptTuple;
        if (Shapeshifting) return true;
        else
        {
            MarkedPlayer = target.PlayerId;
            SendRPC();
            killer.ResetKillCooldown();
            killer.SetKillCooldownV2();
            killer.RPCPlayCustomSound("Clothe");
            return false;
        }
    }
    private bool Shapeshifting;
    public override void OnShapeshift(PlayerControl target)
    {
        Shapeshifting = !Is(target);

        if (!AmongUsClient.Instance.AmHost) return;

        if (!Shapeshifting)
        {
            Player.SetKillCooldownV2();
            return;
        }
        if (MarkedPlayer != byte.MaxValue)
        {
            target = Utils.GetPlayerById(MarkedPlayer);
            MarkedPlayer = byte.MaxValue;
            SendRPC();
            new LateTask(() =>
            {
                if (!(target == null || !target.IsAlive() || target.IsEaten() || target.inVent || !GameStates.IsInTask))
                {
                    Utils.TP(Player.NetTransform, target.GetTruePosition());
                    CustomRoleManager.OnCheckMurder(Player, target);
                }
            }, 1.5f, "Assassin Assassinate");
        }
    }
    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("AssassinMarkButtonText");
        return !Shapeshifting;
    }
    public override bool GetAbilityButtonText(out string text)
    {
        text = GetString("AssassinShapeshiftText");
        return MarkedPlayer != byte.MaxValue && !Shapeshifting;
    }
    public bool OverrideKillButtonSprite(out string buttonName)
    {
        buttonName = "Mark";
        return !Shapeshifting;
    }
    public override bool GetAbilityButtonSprite(out string buttonName)
    {
        buttonName = "Assassinate";
        return MarkedPlayer != byte.MaxValue && !Shapeshifting;
    }
}