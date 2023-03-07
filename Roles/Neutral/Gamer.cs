using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Options;
namespace TOHE.Roles.Neutral;

public static class Gamer
{
    private static readonly int Id = 5060956;
    public static List<byte> playerIdList = new();

    private static Dictionary<byte, int> PlayerHealth;
    private static Dictionary<byte, int> GamerHealth;

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem HealthMax;
    private static OptionItem Damage;
    private static OptionItem SelfHealthMax;
    private static OptionItem SelfDamage;

    public static void SetupCustomOption()
    {
        //玩家只能有一人
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Gamer, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "GamerKillCooldown", new(1f, 180f, 1f), 3f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer]);
        HealthMax = IntegerOptionItem.Create(Id + 15, "GamerHealthMax", new(5, 990, 5), 100, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer])
            .SetValueFormat(OptionFormat.Percent);
        Damage = IntegerOptionItem.Create(Id + 16, "GamerDamage", new(1, 100, 1), 10, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer])
            .SetValueFormat(OptionFormat.Percent);
        SelfHealthMax = IntegerOptionItem.Create(Id + 17, "GamerSelfHealthMax", new(100, 100, 1), 100, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer])
            .SetValueFormat(OptionFormat.Percent);
        SelfDamage = IntegerOptionItem.Create(Id + 18, "GamerSelfDamage", new(1, 100, 1), 35, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer])
            .SetValueFormat(OptionFormat.Percent);
    }
    public static void Init()
    {
        playerIdList = new();
        GamerHealth = new();
        PlayerHealth = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        GamerHealth.TryAdd(playerId, SelfHealthMax.GetInt());
        foreach (var pc in PlayerControl.AllPlayerControls.ToArray().Where(x => !x.Is(CustomRoles.GM) && x.IsAlive()))
            PlayerHealth.TryAdd(pc.PlayerId, HealthMax.GetInt());

        if (!AmongUsClient.Instance.AmHost) return;

        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static void CanUseVent(PlayerControl player)
    {
        bool gamer_canUse = CanVent.GetBool();
        DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.ToggleVisible(gamer_canUse && !player.Data.IsDead);
        player.Data.Role.CanVent = gamer_canUse;
    }
    public static bool CheckGamerMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || !killer.Is(CustomRoles.Gamer) || target.Is(CustomRoles.Gamer)) return false;
        killer.SetKillCooldown();

        if (PlayerHealth[target.PlayerId] - Damage.GetInt() < 1)
        {
            PlayerHealth.Remove(target.PlayerId);
            Utils.NotifyRoles(killer);
            return false;
        }

        PlayerHealth[target.PlayerId] -= Damage.GetInt();
        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        Utils.NotifyRoles(killer);

        Logger.Info($"{killer.GetNameWithRole()} 对玩家 {target.GetNameWithRole()} 造成了 {Damage.GetInt()} 点伤害", "Gamer");
        return true;
    }
    public static bool CheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || !target.Is(CustomRoles.Gamer) || killer.Is(CustomRoles.Gamer)) return false;
        killer.SetKillCooldown();

        if (GamerHealth[target.PlayerId] - SelfDamage.GetInt() < 1)
        {
            GamerHealth.Remove(target.PlayerId);
            Utils.NotifyRoles(target);
            return false;
        }

        GamerHealth[target.PlayerId] -= SelfDamage.GetInt();
        RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
        killer.RpcGuardAndKill(target);
        Utils.NotifyRoles(target);

        Logger.Info($"{killer.GetNameWithRole()} 对玩家 {target.GetNameWithRole()} 造成了 {SelfDamage.GetInt()} 点伤害", "Gamer");
        return true;
    }
    public static string TargetMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.Is(CustomRoles.Gamer)) return "";
        if (seer.PlayerId == target.PlayerId)
        {
            var GetValue = GamerHealth.TryGetValue(target.PlayerId, out var value);
            return GetValue && value > 0 ? Utils.ColorString(GetColor(value, true), $"【{value}/{SelfHealthMax.GetInt()}】") : "";
        }
        else
        {
            var GetValue = PlayerHealth.TryGetValue(target.PlayerId, out var value);
            return GetValue && value > 0 ? Utils.ColorString(GetColor(value), $"【{value}/{HealthMax.GetInt()}】") : "";
        }
    }
    private static Color32 GetColor(float Health, bool self = false)
    {
        var rate = (Health / (self ? SelfHealthMax.GetInt() : HealthMax.GetInt())) * 100;
        if (rate <= 10) return new Color32(255, 3, 1, byte.MaxValue);
        if (rate <= 20) return new Color32(254, 51, 0, byte.MaxValue);
        if (rate <= 30) return new Color32(255, 101, 0, byte.MaxValue);
        if (rate <= 40) return new Color32(255, 150, 3, byte.MaxValue);
        if (rate <= 50) return new Color32(254, 202, 1, byte.MaxValue);
        if (rate <= 60) return new Color32(253, 255, 4, byte.MaxValue);
        if (rate <= 70) return new Color32(206, 255, 0, byte.MaxValue);
        if (rate <= 80) return new Color32(154, 255, 0, byte.MaxValue);
        if (rate <= 90) return new Color32(101, 255, 0, byte.MaxValue);
        if (rate <= 100) return new Color32(53, 255, 0, byte.MaxValue);
        return new Color32();
    }
}