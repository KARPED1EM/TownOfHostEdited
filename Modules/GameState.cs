using System;
using System.Collections.Generic;

namespace TownOfHost
{
    public static class PlayerState
    {

        static PlayerState()
        {
            Init();
        }

        public static void Init()
        {
            players = new();
            isDead = new();
            deathReasons = new();
            taskState = new();

            foreach (var p in PlayerControl.AllPlayerControls)
            {
                players.Add(p.PlayerId);
                isDead.Add(p.PlayerId, false);
                deathReasons.Add(p.PlayerId, DeathReason.etc);
                taskState.Add(p.PlayerId, new());
            }

        }
        public static List<byte> players = new List<byte>();
        public static Dictionary<byte, bool> isDead = new Dictionary<byte, bool>();
        public static Dictionary<byte, DeathReason> deathReasons = new Dictionary<byte, DeathReason>();
        public static Dictionary<byte, TaskState> taskState = new();
        public static void setDeathReason(byte p, DeathReason reason) { deathReasons[p] = reason; }
        public static DeathReason getDeathReason(byte p) { return deathReasons.TryGetValue(p, out var reason) ? reason : DeathReason.etc; }
        public static bool isSuicide(byte p) { return deathReasons[p] == DeathReason.Suicide; }
        public static void InitTask(PlayerControl player)
        {
            taskState[player.PlayerId].Init(player);
        }
        public static TaskState UpdateTask(PlayerControl player)
        {
            var task = taskState[player.PlayerId];
            task.Update(player);
            return task;
        }
        public enum DeathReason
        {
            Kill,
            Vote,
            Suicide,
            Spell,
            Bite,
            Bombed,
            Misfire,
            Torched,
            Disconnected,
            etc = -1
        }
    }
    public class TaskState
    {
        public int AllTasksCount;
        public int CompletedTasksCount;
        public bool hasTasks;
        public int RemainingTasksCount => AllTasksCount - CompletedTasksCount;
        public bool doExpose => RemainingTasksCount <= Options.SnitchExposeTaskLeft && hasTasks;
        public bool isTaskFinished => RemainingTasksCount <= 0 && hasTasks;
        public TaskState()
        {
            this.AllTasksCount = -1;
            this.CompletedTasksCount = 0;
            this.hasTasks = false;
        }

        public void Init(PlayerControl player)
        {
            Logger.info($"{player.name}: InitTask", "TaskCounts");
            if (player == null || player.Data == null || player.Data.Tasks == null) return;
            if (!Utils.hasTasks(player.Data, false)) return;
            hasTasks = true;
            AllTasksCount=player.Data.Tasks.Count;

            //役職ごとにタスク量の調整を行う
            var adjustedTasksCount = AllTasksCount;
            switch (player.getCustomRole())
            {
                case CustomRoles.MadSnitch:
                    adjustedTasksCount = Options.MadSnitchTasks.GetInt();
                    break;
                default:
                    break;
            }
            //タスク数が通常タスクより多い場合は再設定が必要
            AllTasksCount = Math.Min(adjustedTasksCount, AllTasksCount);
            Logger.info($"{player.name}: {CompletedTasksCount}/{AllTasksCount}", "TaskCounts");
        }
        public void Update(PlayerControl player)
        {
            Logger.info($"{player.name}: UpdateTask", "TaskCounts");
            if (player == null || player.Data == null || player.Data.Tasks == null) return;
            if (!Utils.hasTasks(player.Data, false)) return;
            //初期化出来ていなかったら初期化
            if(AllTasksCount==-1)Init(player);
            //クリアしてたらカウントしない
            if (CompletedTasksCount >= AllTasksCount) return;

            foreach (var task in player.Data.Tasks)
            {
                if (task.Complete) CompletedTasksCount++;
            }
            //調整後のタスク量までしか表示しない
            CompletedTasksCount = Math.Min(AllTasksCount, CompletedTasksCount);
            Logger.info($"{player.name}: {CompletedTasksCount}/{AllTasksCount}", "TaskCounts");
        }
    }
}
