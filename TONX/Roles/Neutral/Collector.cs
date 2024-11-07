using AmongUs.GameOptions;
using System.Linq;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using System;
using System.Collections.Generic;
using Hazel;
using TONX.Modules;
using Sentry.Internal.Http;

namespace TONX.Roles.Neutral;

public sealed class Collector : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
       SimpleRoleInfo.Create(
            typeof(Collector),
            player => new Collector(player),
            CustomRoles.Collector,
         () => RoleTypes.Scientist,
            CustomRoleTypes.Neutral,
            75_1_2_1200,
             SetupCustomOption,
            "colle|¼¯Æ±|¼ÄÆ±",
            "#9d8892"

        );
    public Collector(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        CollectVote = 0;
        CollectorVoteFor = null;
    }
    private int CollectVote;
    private PlayerControl CollectorVoteFor;
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(CollectVote);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        int Num = reader.ReadInt32();
        CollectVote = Num;
    }
    enum OptionName
    {
        CollectorCollectAmount,
    }
    public static OptionItem CollectorCollectAmount;
    private static void SetupCustomOption()
    {
        CollectorCollectAmount = IntegerOptionItem.Create(RoleInfo, 10, OptionName.CollectorCollectAmount, new(1, 999, 1), 20, false)
            .SetValueFormat(OptionFormat.Votes);
    }

    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);

    public override string GetProgressText(bool comms = false)
    {
        int VoteAmount = CollectVote;
        int CollectNum = CollectorCollectAmount.GetInt();
        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Collector).ShadeColor(0.25f), $"({VoteAmount}/{CollectNum})");
    }
    public override bool CheckVoteAsVoter(PlayerControl votedFor)
    {
        if (votedFor == null || !Player.IsAlive()) return true;
        CollectorVoteFor = votedFor;
        return true;
    }

    public override (byte? votedForId, int? numVotes, bool doVote) ModifyVote(byte voterId, byte sourceVotedForId, bool isIntentional)
    {
        var (votedForId, numVotes, doVote) = base.ModifyVote(voterId, sourceVotedForId, isIntentional);
        if (CollectorVoteFor == null) return (votedForId, numVotes, doVote);
        if (votedForId == CollectorVoteFor.PlayerId)
        {
            CollectVote += (int)numVotes;
            SendRPC();
        }
        return (votedForId, numVotes, doVote);
    }
    public override void AfterMeetingTasks()
    {
        CollectorVoteFor = null;
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || CollectVote < CollectorCollectAmount.GetInt()) return;
        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Collector);
        CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
    }
}
