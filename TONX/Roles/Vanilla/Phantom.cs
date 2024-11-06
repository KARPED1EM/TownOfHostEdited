using AmongUs.GameOptions;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;

namespace TONX.Roles.Vanilla;

public sealed class Phantom : RoleBase, IImpostor
{
    public Phantom(PlayerControl player) : base(RoleInfo, player) { }
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.CreateForVanilla(typeof(Phantom), player => new Phantom(player), RoleTypes.Phantom);
}
