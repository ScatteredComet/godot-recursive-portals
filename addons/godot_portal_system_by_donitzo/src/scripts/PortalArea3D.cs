using Godot;

namespace Portals;

public partial class PortalArea3D : Area3D
{
    [Export] public Node3D TeleportableRoot {get; private set;}

    public const int DefaultPortalableObjectLayer = 6;

    public Portal lastPortalExited;

    public override void _Ready()
    {
        base._Ready();

        
        Monitoring = false;
        Monitorable = true;

        CollisionLayer = 0;
        CollisionMask = 0;
        
        SetCollisionLayerValue(DefaultPortalableObjectLayer, true);
    }
}