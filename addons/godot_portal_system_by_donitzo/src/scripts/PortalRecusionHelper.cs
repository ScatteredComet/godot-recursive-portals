using Godot;
using System;

namespace Portals;

public static class PortalRecusionHelper
{
    // calculates the global transform of the image of portal1 as viewed through portal2 (or the other way around idk)
    public static Transform3D PortalImage(MeshInstance3D portal1, MeshInstance3D portal2, int repeats = 1)
    {
        Transform3D relative = portal2.GlobalTransform * portal1.GlobalTransform.AffineInverse().Rotated(portal1.Basis.Y, (float)Math.PI);

        Transform3D result = portal2.GlobalTransform;
        
        for (int i = 0; i < repeats; i++)
        {
            result = relative * result;
        }
        return result;
    }
}
