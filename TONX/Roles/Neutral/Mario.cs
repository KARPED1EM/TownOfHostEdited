using AmongUs.GameOptions;
using Hazel;

using TONX.Modules;
using TONX.Roles.Core;
using static TONX.Translator;

namespace TONX.Roles.Neutral;
public sealed class Mario : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
       SimpleRoleInfo.Create(
            typeof(Mario),
            player => new Mario(player),
            CustomRoles.Mario,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Neutral,
            50850,
            SetupOptionItem,
            "ma|馬里奧|马力欧",
            "#ff6201",
            experimental: true
        );
    public Mario(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    { }

    private static OptionItem OptionVentNums;
    enum OptionName
    {
        MarioVentNumWin
    }

    private int VentedTimes;
    private static void SetupOptionItem()
    {
        OptionVentNums = IntegerOptionItem.Create(RoleInfo, 10, OptionName.MarioVentNumWin, new(1, 999, 1), 55, false);
    }
    public override void Add() => VentedTimes = 0;
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(VentedTimes);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        VentedTimes = reader.ReadInt32();
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }
    public override bool GetAbilityButtonText(out string text)
    {
        text = GetString("MarioVentButtonText");
        return true;
    }
    public override int OverrideAbilityButtonUsesRemaining() => OptionVentNums.GetInt() - VentedTimes;
    public override string GetProgressText(bool comms = false) => Utils.ColorString(Utils.ShadeColor(RoleInfo.RoleColor, 0.25f), $"({VentedTimes}/{OptionVentNums.GetInt()})");
    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        VentedTimes++;
        SendRPC();
        Utils.NotifyRoles(Player);

        if (VentedTimes % 5 == 0) CustomSoundsManager.Play("MarioCoin");
        else CustomSoundsManager.Play("MarioJump");

        if (VentedTimes >= OptionVentNums.GetInt())
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Mario);
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
        }

        return true;
    }
}