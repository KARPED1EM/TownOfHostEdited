using AmongUs.GameOptions;
using TONX.Roles.Core;

namespace TONX.Roles.Vanilla;

public sealed class Tracker : RoleBase
{
    public Tracker(PlayerControl player) : base(RoleInfo, player) { }
    public readonly static SimpleRoleInfo RoleInfo = SimpleRoleInfo.CreateForVanilla(typeof(Tracker), player => new Tracker(player), RoleTypes.Tracker, "#8cffff");
}
