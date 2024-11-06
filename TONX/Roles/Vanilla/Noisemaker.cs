using AmongUs.GameOptions;
using TONX.Roles.Core;

namespace TONX.Roles.Vanilla;

public sealed class Noisemaker : RoleBase
{
    public readonly static SimpleRoleInfo RoleInfo = 
        SimpleRoleInfo.CreateForVanilla(
            typeof(Noisemaker), 
            player => new Noisemaker(player), 
            RoleTypes.Noisemaker);
    public Noisemaker(PlayerControl player) 
        : base(
            RoleInfo, 
            player) 
    { }

}
