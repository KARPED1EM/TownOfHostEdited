using AmongUs.GameOptions;
using Hazel;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using UnityEngine;
using static TONX.Translator;

namespace TONX.Roles.Impostor;
public sealed class Eraser : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Eraser),
            player => new Eraser(player),
            CustomRoles.Eraser,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            4600,
            SetupOptionItem,
            "er|抹除者|抹除",
            experimental: true
        );
    public Eraser(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    static OptionItem OptionEraseLimit;
    enum OptionName
    {
        EraseLimit
    }

    private int EraseLimit;
    private byte PlayerToErase;
    private static void SetupOptionItem()
    {
        OptionEraseLimit = IntegerOptionItem.Create(RoleInfo, 10, OptionName.EraseLimit, new(1, 15, 1), 2, false)
            .SetValueFormat(OptionFormat.Times);
    }
    public override void Add()
    {
        EraseLimit = OptionEraseLimit.GetInt();
        PlayerToErase = byte.MaxValue;
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(EraseLimit);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        EraseLimit = reader.ReadInt32();
    }
    public override string GetProgressText(bool comms = false) => Utils.ColorString(EraseLimit >= 1 ? Color.red : Color.gray, $"({EraseLimit})");
    public override bool CheckVoteAsVoter(PlayerControl votedFor)
    {
        if (votedFor == null || !Player.IsAlive() || EraseLimit < 1 || PlayerToErase != byte.MaxValue) return true;

        if (Is(votedFor))
        {
            // 抹除自己
            ShowMsg(GetString("EraserEraseSelf") + GetString("TargetInvalidAndYouShouldChooseAnotherTarget"));
        }
        else if (votedFor.Is(CustomRoleTypes.Neutral))
        {
            // 抹除中立阵营玩家
            ShowMsg(string.Format(GetString("EraserEraseNeutralNotice"), votedFor.GetRealName()) + GetString("TargetInvalidAndYouShouldChooseAnotherTarget"));
        }
        else
        {
            // 正常抹除
            EraseLimit--;
            SendRPC();

            PlayerToErase = votedFor.PlayerId;

            ShowMsg(string.Format(GetString("EraserEraseNotice"), votedFor.GetRealName()) + GetString("SkillDoneAndYouCanVoteNormallyNow"));
        }

        return false;

        void ShowMsg(string msg)
        {
            Player.ShowPopUp(msg);
            Utils.SendMessage(msg, Player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));
        }
    }
    public override void OnStartMeeting()
    {
        PlayerToErase = byte.MaxValue;
    }
    public override void AfterMeetingTasks()
    {
        if (PlayerToErase == byte.MaxValue) return;
        var player = Utils.GetPlayerById(PlayerToErase);
        if (player == null) return;
        player.RpcSetCustomRole(player.GetCustomRole().GetRoleInfo().BaseRoleType.Invoke().GetCustomRoleTypes());
        NameNotifyManager.Notify(player, GetString("LostRoleByEraser"));
        Logger.Info($"{player.GetNameWithRole()} 被擦除了", "Eraser.AfterMeetingTasks");

    }
}
