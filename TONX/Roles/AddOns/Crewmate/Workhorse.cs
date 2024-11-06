using System;
using System.Collections.Generic;
using TONX.Attributes;
using TONX.Roles.Core;
using UnityEngine;
using static TONX.Options;

namespace TONX.Roles.AddOns.Crewmate;
public static class Workhorse
{
    private static readonly int Id = 80400;
    public static Color RoleColor = Utils.GetRoleColor(CustomRoles.Workhorse);
    public static List<byte> playerIdList = new();
    private static OptionItem OptionAssignOnlyToCrewmate;
    private static OptionItem OptionSnitchCanBeWorkhorse;
    private static OptionItem OptionNumLongTasks;
    private static OptionItem OptionNumShortTasks;
    public static bool AssignOnlyToCrewmate;
    public static bool SnitchCanBeWorkhorse;
    public static int NumLongTasks;
    public static int NumShortTasks;
    public static void SetupCustomOption()
    {
        SetupAddonOptions(Id, TabGroup.Addons, CustomRoles.Workhorse, RoleSpwanToggle, false);
        OptionAssignOnlyToCrewmate = BooleanOptionItem.Create(Id + 10, "AssignOnlyToCrewmate", true, TabGroup.Addons, false).SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse]);
        OptionSnitchCanBeWorkhorse = BooleanOptionItem.Create(Id + 13, "SnitchCanBeWorkhorse", false, TabGroup.Addons, false).SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse]);
        OptionNumLongTasks = IntegerOptionItem.Create(Id + 11, "WorkhorseNumLongTasks", new(0, 5, 1), 1, TabGroup.Addons, false).SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse])
            .SetValueFormat(OptionFormat.Pieces);
        OptionNumShortTasks = IntegerOptionItem.Create(Id + 12, "WorkhorseNumShortTasks", new(0, 5, 1), 1, TabGroup.Addons, false).SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse])
            .SetValueFormat(OptionFormat.Pieces);
    }
    [GameModuleInitializer]
    public static void Init()
    {
        playerIdList = new();

        AssignOnlyToCrewmate = OptionAssignOnlyToCrewmate.GetBool();
        SnitchCanBeWorkhorse = OptionSnitchCanBeWorkhorse.GetBool();
        NumLongTasks = OptionNumLongTasks.GetInt();
        NumShortTasks = OptionNumShortTasks.GetInt();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static bool IsThisRole(byte playerId) => playerIdList.Contains(playerId);
    public static (bool, int, int) TaskData => (false, NumLongTasks, NumShortTasks);
    private static bool IsAssignTarget(PlayerControl pc)
    {
        if (!pc.IsAlive() || IsThisRole(pc.PlayerId)) return false;
        var taskState = pc.GetPlayerTaskState();
        if (taskState.CompletedTasksCount < taskState.AllTasksCount) return false;
        if (!Utils.HasTasks(pc.Data)) return false;
        if (pc.Is(CustomRoles.Snitch) && !SnitchCanBeWorkhorse) return false;
        if (AssignOnlyToCrewmate)
            return pc.Is(CustomRoleTypes.Crewmate);
        return !OverrideTasksData.AllData.ContainsKey(pc.GetCustomRole()); //タスク上書きオプションが無い
    }
    public static bool OnCompleteTask(PlayerControl pc)
    {
        if (playerIdList.Count >= CustomRoles.Workhorse.GetCount()) return true;
        if (!IsAssignTarget(pc)) return true;

        pc.RpcSetCustomRole(CustomRoles.Workhorse);
        var taskState = pc.GetPlayerTaskState();
        taskState.AllTasksCount += NumLongTasks + NumShortTasks;

        if (AmongUsClient.Instance.AmHost)
        {
            Add(pc.PlayerId);
            pc.Data.RpcSetTasks(Array.Empty<byte>()); //タスクを再配布
            pc.SyncSettings();
            Utils.NotifyRoles();
        }

        return false;
    }
}