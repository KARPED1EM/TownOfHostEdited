using AmongUs.GameOptions;
using System.Collections.Generic;

using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using static TONX.Translator;

namespace TONX.Roles.Impostor;
public sealed class Capitalist : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Capitalist),
            player => new Capitalist(player),
            CustomRoles.Capitalist,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            4300,
            SetupOptionItem,
            "ca|資本家|资本|资本主义",
            experimental: true
        );
    public Capitalist(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        NumShortTasks = new();
        TasksWaitToAdd = new();
    }

    static OptionItem OptionKillCooldown;
    enum OptionName
    {
        CapitalistSkillCooldown,
    }

    public static Dictionary<byte, int> NumShortTasks;
    public static Dictionary<byte, int> TasksWaitToAdd;
    public bool IsKiller { get; private set; } = false;
    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.CapitalistSkillCooldown, new(2.5f, 180f, 2.5f), 12.5f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public float CalculateKillCooldown() => OptionKillCooldown.GetFloat();
    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("CapitalistButtonText");
        return true;
    }
    public static int GetShortTasks(byte playerId, int shortTasks) => (NumShortTasks != null && NumShortTasks.TryGetValue(playerId, out var x)) ? x : shortTasks;
    public static bool OnCompleteTask(PlayerControl pc)
    {
        if (!CustomRoles.Capitalist.IsExist(true)) return true;
        if (pc.Is(CustomRoles.Workhorse)) return true;
        if (!Utils.HasTasks(pc.Data) || pc.AllTasksCompleted()) return true;
        if (TasksWaitToAdd == null || !TasksWaitToAdd.ContainsKey(pc.PlayerId)) return true;

        var taskNum = NumShortTasks.TryGetValue(pc.PlayerId, out var x) ? x : Main.NormalOptions.NumShortTasks;
        NumShortTasks[pc.PlayerId] = taskNum + TasksWaitToAdd[pc.PlayerId];

        var taskState = pc.GetPlayerTaskState();
        taskState.AllTasksCount += TasksWaitToAdd[pc.PlayerId];

        TasksWaitToAdd.Remove(pc.PlayerId);

        if (AmongUsClient.Instance.AmHost)
        {
            pc.Data.RpcSetTasks(new byte[0]);
            pc.SyncSettings();
            Utils.NotifyRoles();
        }

        return false;
    }
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        var target = info.AttemptTarget;
        if (target.Is(CustomRoles.Workhorse)) return false;
        if (!Utils.HasTasks(target.Data)) return false;
        if (target.AllTasksCompleted()) return false;

        TasksWaitToAdd.TryAdd(target.PlayerId, 0);
        TasksWaitToAdd[target.PlayerId]++;

        Player.SetKillCooldownV2();
        RPC.PlaySoundRPC(Player.PlayerId, Sounds.TaskUpdateSound);

        return false;
    }
}