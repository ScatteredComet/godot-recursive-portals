/*
    ORIGINAL LICENSE DETAILS
    Asset: Godot Simple Portal System
    File: advanced_portal_teleport.gd
	Description: An area which teleports a node through the parent node's portal. Checks entry velocity and handles RigidBody3D and CharacterBody3D physics. Can also handle a portal clone of the node if specified.
	Instructions: For detailed documentation, see the README or visit: https://github.com/Donitzo/godot-simple-portal-system
	Repository: https://github.com/Donitzo/godot-simple-portal-system
	License: CC0 License

    MODIFICATION DETAILS
    Converted to C# and modified by OffByTwo
*/

using Godot;
using System;

namespace Portals;

public partial class AdvancedPortalTeleport : Area3D
{
    [Export] private bool checkIfMovingTowards = true;
    // set to be > 0 for this to have an affect;
    [Export] private float exitPushVelocity = -1f;
    // Seconds to keep portal clones visible after the node leaves the teleporter.
    [Export] private float cloneKeepAliveSeconds = .1f;

    [Export] private Portal parentPortal;
    
    public override void _Ready()
    {
        base._Ready();
        
        AreaEntered += OnAreaEntered;
        AreaExited += OnAreaExited;

        // set up layers

        Monitorable = false;
        Monitoring = true;
        CollisionLayer = 0;
        CollisionMask = 0;

        SetCollisionMaskValue(PortalArea3D.DefaultPortalableObjectLayer, true);
    }

    // Try to teleport the crossing node, and return false if it fails (e.g. if the crossingNode is not moving towards the portal)
    private bool TryTeleport(Node3D teleportableRoot)
    {
        // Check if the node is moving towards the portal
        if (checkIfMovingTowards)
        {
            if (teleportableRoot is RigidBody3D rigidbody)
            {
                Vector3 localVelocity = parentPortal.GlobalBasis.Inverse() * rigidbody.LinearVelocity;
                if (localVelocity.Z >= 0)
                {
                    return false;
                }
            }
            else if (teleportableRoot is CharacterBody3D cb)
            {
                Vector3 localVelocity = parentPortal.GlobalBasis.Inverse() * cb.Velocity;
                if (localVelocity.Z >= 0)
                {
                    return false;
                }
            }
            else
            {
                // only support the above nodes going through a portal
                // throw new NotImplementedException();
            }
        }

        if (teleportableRoot is RigidBody3D rb)
        {
            rb.LinearVelocity = parentPortal.RealToExitDirection(rb.LinearVelocity);
            rb.AngularVelocity *= parentPortal.RealToExitTransform(Transform3D.Identity, parentPortal.exitPortal).Basis.Inverse();

            if (exitPushVelocity > 0)
            {
                Vector3 exitForward = parentPortal.exitPortal.GlobalBasis.Z.Normalized();
                rb.LinearVelocity += exitForward * exitPushVelocity;
            }
        }
        else if (teleportableRoot is CharacterBody3D cb)
        {
            cb.Velocity = parentPortal.RealToExitDirection(cb.Velocity);

            if (exitPushVelocity > 0)
            {
                Vector3 exitForward = parentPortal.exitPortal.GlobalBasis.Z.Normalized();
                cb.Velocity += exitForward * exitPushVelocity;
            }
        }

        teleportableRoot.GlobalTransform = parentPortal.RealToExitTransform(teleportableRoot.GlobalTransform, parentPortal.exitPortal);

        return true;
    }

    [Export] private RayCast3D GetNearbyNormalRayCast3D;

    private Vector3? GetNearbyNormal()
    {
        RayCast3D exitRayCast = parentPortal.exitPortal.advancedPortalTeleport.GetNearbyNormalRayCast3D;

        if (exitRayCast is null)
        {
            return null;
        }

        if (!exitRayCast.IsColliding())
        {
            return null;
        }

        return exitRayCast.GetCollisionNormal();
    }














    // signal recievers
    private void OnAreaEntered(Area3D area3D)
    {
        if (area3D is not PortalArea3D portalArea3D)
        {
            return;
        }

        if (portalArea3D.lastPortalExited == parentPortal)
        {
            return;
        }

        Node3D teleportableRoot = portalArea3D.TeleportableRoot;

        GetTree().PhysicsInterpolation = false;

        if (TryTeleport(teleportableRoot) == true)
        {
            portalArea3D.lastPortalExited = parentPortal.exitPortal;
        }

        GetTree().PhysicsInterpolation = (bool)ProjectSettings.GetSetting("physics/common/physics_interpolation");        
    }

    private void OnAreaExited(Area3D area3D)
    {
        if (area3D is not PortalArea3D portalArea3D)
        {
            return;
        }

        if (portalArea3D.lastPortalExited == parentPortal)
        {
            portalArea3D.lastPortalExited = null;
        }
    }
}