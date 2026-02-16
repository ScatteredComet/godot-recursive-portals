/*
    ORIGINAL LICENSE DETAILS
    Asset: Godot Simple Portal System
    File: portal.gd
    Description: A simple portal system for viewport-based portals in Godot 4.
    Instructions: For detailed documentation, see the README or visit: https://github.com/Donitzo/godot-simple-portal-system
    Repository: https://github.com/Donitzo/godot-simple-portal-system
    License: CC0 License

    MODIFICATION DETAILS
    Converted to C# and modified by OffByTwo
*/

using System;
using System.Collections.Generic;
using Godot;

namespace Portals;

public static class PortalRayCasting
{
    public const string GroupName = "Portals";
}

public partial class Portal : MeshInstance3D
{
    // The delay between the main viewport changing size and the portal viewport resizing.
    private const float ResizeThrottleSeconds = .1f;

    // The minimum camera near clipping distance.
    private const float ExitCameraNearMin = .01f;

    // The portal mesh's local bounding box.
    private Aabb meshAABB => Mesh.GetAabb();

    // The vertical resolution of the portal viewport which covers the entire screen not just the portal mesh. Use 0 to use the real resolution.
    [Export] private int verticalViewportResolution = 0;

    // Portals further away than this won't have their viewports rendered.
    [Export] private float disableViewportDistance = 40;

    // Whether to destroy the disabled viewport to save texture memory. Useful when you have a lot of portals. The viewport is re/-created when within disable_viewport_distance and visible.
    [Export] private bool destroyDisabledViewport = true;

    [Export] private float fadeOutDistanceMax = 30;
    [Export] private float fadeOutDistanceMin = 28;
    [Export] private Color fadeOutColor = new(1, 1, 1);

    // < 1 means the exit is smaller than the entrance.
    [Export] private float exitScale = 1.0f;

    // A value subtracted from the exit camera near clipping plane. Useful for handling clipping issues.
    [Export] private float exitNearSubtract = .05f;

    // Leave unset to use the default 3D camera.
    [Export] private Camera3D mainCamera;

    // Leave unset to use the default environment.
    // Modified by ScatteredComet: replaced by GetViewport().World3D.Environment
    // [Export] private Godot.Environment exitEnvironment;

    // Leave unset to use this portal as an exit only.
    [Export] public Portal exitPortal;

    // The viewport rendering the portal surface
    // Modified by ScatteredComet: replaced by passSubViewports (List)
    // private SubViewport passSubViewport;

    private float secondsUntilResize;

    // added by ScatteredComet
    // recursive stuff

    // recursionLimit = 1 corresponds to not being able to see portals in portals at all. seeing a portal in a portal corresponds to a recursion limit of 2, etc 
    // this can be up to and including 6 as of this commit (but can be extended indefinitely by creating more write / read pass material / shaders)
    private const int recursionLimit = 6;

    private const string ShaderTextureName = "camera_texture";

    // A list of the virtual cameras; there is one for each portal mesh (including the original portals)
    private List<Camera3D> passCameras = [];
    // A list of the subviewports used to render each of the camera textures (again, 1 for each portal mesh)
    private List<SubViewport> passSubViewports = [];
    // a list of virtual portals for this portal. index 0 is the exit portal, index 1 is the exit portal duplicated and transformed twice (so 1 "iteration" further away from this portal) etc. These can be visualised using the debugVisible bool.
    private List<MeshInstance3D> passPortals = [];

    // --- DEBUGGING --- //

    // show the position of the camera relative to the virtual portals. Set as null to hide.
    [Export] private PackedScene cameraDebugMeshScene;
    private const int CameraDebugMeshVisualLayer = 5;
    
    // set true to see virtual portals. Can set a different colour for each portal's corresponding virtual portals
    [Export] private bool debugVisible = false;
    [Export] private Color debugColor = new(1, 0, 0);

    public override void _Ready()
    {
        base._Ready();

        Startup();
    }

    private bool paused = false;

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);
        
        if (Input.IsActionJustPressed("debug_pause_portals"))
        {
            paused = !paused;
        }
    }

    // call this once you've set up the portal (if using delayed_ready)
    public void Startup()
    {
        GD.Print($"{Name} is starting up");

        ProcessPhysicsPriority = 1000;
        
        if (!IsInsideTree())
        {
            GD.PushError($"[Portals] :The portal {Name} is not inside a SceneTree.");
        }

        // An exit-free portal does not need to do anything
        if (exitPortal is null)
        {
            GD.PushError($"[Portals] : Portal {Name} deactivating as no exit_portal is set.");
            Visible = false;
            SetProcess(false);
            return;
        }

        if (!exitPortal.IsInsideTree() || exitPortal.GetTree() != GetTree())
        {
            GD.PushError($"[Portals] : The exit_portal {exitPortal.Name} for {Name} is not inside the same SceneTree.");
        }

        // Non-uniform parent scaling can introduce skew which isn't compensated for
        if (GetParent() is not null)
        {
            Vector3 parentScale = (GetParent() as Node3D).GlobalTransform.Basis.Scale;

            if (Math.Abs(parentScale.X - parentScale.Y) > .01f || Math.Abs(parentScale.X - parentScale.Z) > .01f)
            {
                GD.PushWarning($"[Portals] : The parent of {Name} is not uniformly scaled. The portal will not work correctly.");
            }
        }

        // The portals should be updated last so the main camera has its final position

        AddToGroup(PortalRayCasting.GroupName);

        mainCamera ??= GetViewport().GetCamera3D();

        // The portal shader renders the viewport on-top of the portal mesh in screen-space
        MaterialOverride = GD.Load("res://addons/godot_portal_system_by_donitzo/src/shaders/PortalShaderMaterial.tres") as ShaderMaterial;
        
        // this MUST be a deep copy (I believe to copy over nextPasses) - otherwise only the portal which renders SECOND in the scene tree will render correctly (as it overwrites textures in the first portal's shader)
        // This line makes sure that textures are not being shared between different portals, i.e. that each portal has its own unique shader which contains its own unique textures.
        MaterialOverride = MaterialOverride.Duplicate(true) as ShaderMaterial; // dupe so camera texture can be unique for each instance

        // Create the viewport when _ready if it's not destroyed when disabled. This may potentially get rid of the initial lag when the viewport is first created at the cost of texture memory.
        if (!destroyDisabledViewport)
        {
            CreateViewports();
        }

        GetViewport().SizeChanged += HandleResize;

        // passCameras.Add(mainCamera);

        SetupPortalRecursion();
    }

    private void HandleResize()
    {
        secondsUntilResize = ResizeThrottleSeconds;
    }

    // Create the viewport for the portal surface
    private void CreateViewports()
    {
        // create a linear environment (avoid applying tonemaps & adjustments multiple times)
        Godot.Environment linearEnvironment = GetViewport().World3D.Environment.Duplicate() as Godot.Environment;
        linearEnvironment.TonemapMode = Godot.Environment.ToneMapper.Linear;
        linearEnvironment.AdjustmentEnabled = false;

        ShaderMaterial currentPassMaterial = MaterialOverride as ShaderMaterial;

        for (int i = 0; i < recursionLimit; i++)
        {
            SubViewport passSubViewport = new()
            {
                Name = $"PortalSubViewport-Pass{i}",
                // RenderTargetClearMode = SubViewport.ClearMode.Once
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                Size = (Vector2I)GetViewport().GetVisibleRect().Size
            };

            currentPassMaterial.SetShaderParameter(ShaderTextureName, passSubViewport.GetTexture());
        
            // Create the exit camera which renders the portal surface for the viewport
            Camera3D passCamera = new()
            {
                Name = $"PortalCamera-Pass{i}",
                Environment = linearEnvironment
            };

            passCamera.SetCullMaskValue(CameraDebugMeshVisualLayer, false); // dont show debug meshes in the portals (it gets messy)

            passCameras.Add(passCamera);
            passSubViewports.Add(passSubViewport);

            AddChild(passSubViewport);
            passSubViewport.AddChild(passCamera);

            currentPassMaterial = currentPassMaterial.NextPass as ShaderMaterial;

            // debug visuals

            if (cameraDebugMeshScene is not null)
            {
                MeshInstance3D m = cameraDebugMeshScene.Instantiate() as MeshInstance3D;
                passCamera.AddChild(m);
                m.SetSurfaceOverrideMaterial(0, m.GetActiveMaterial(0).Duplicate() as StandardMaterial3D);
                (m.GetActiveMaterial(0) as StandardMaterial3D).AlbedoColor = debugColor;
            }
        }

        // Resize the viewport on the next _process
        secondsUntilResize = 0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (paused)
        {
            return;
        }

        if (passSubViewports.Count == 0)
        {
            CreateViewports();
        }

        // Move the exit camera relative to the exit portal based on the main camera's position relative to the entrance portal    

        for (int i = 0; i < recursionLimit; i++)
        {
            passCameras[i].GlobalTransform = RealToExitTransform(mainCamera.GlobalTransform, exitPortal.passPortals[i]);
            
            try
            {
                SetCameraClippingPlane(exitPortal.passCameras[i], this);
            }
            catch (ArgumentOutOfRangeException)
            {
                // above line will fail on ready as only 1 portal has had its passCameras setup.
                // GD.Print($"{i}");
            }
        }
    }

    private void SetCameraClippingPlane(Camera3D passCamera, MeshInstance3D passPortal)
    {
        Aabb passAABB = passPortal.GetAabb();

        float minDepth = float.PositiveInfinity;

        for (int cornerIndex = 4; cornerIndex < 8; cornerIndex++)
        {
           Vector3 corner = passAABB.GetEndpoint(cornerIndex);

           corner.Z = 0; // flatten to portal surface
           
           Vector3 world = passPortal.ToGlobal(corner);

           Vector3 view = passCamera.ToLocal(world);

           float depth = -view.Z;

           minDepth = Math.Min(minDepth, depth);
        }

        passCamera.Near = minDepth;

        return;
    }

    private const int WritePassLayer = 10;

    // portal recusions
    private void SetupPortalRecursion()
    {
        passPortals.Add(this);

        for (int i = 1; i < recursionLimit; i++)
        {
            MeshInstance3D recursedPortal = new()
            {
                Mesh = this.Mesh.Duplicate() as Mesh,

                MaterialOverride = GD.Load($"res://addons/godot_portal_system_by_donitzo/src/shaders/recursive_write_standardmaterial3Ds/write_pass_{i}.tres") as StandardMaterial3D
            };

            // if (debugVisible)
            // {
            //     (recursedPortal.MaterialOverride as StandardMaterial3D).AlbedoColor = debugColor;
            // }
            // else
            // {
            //     (recursedPortal.MaterialOverride as StandardMaterial3D).AlbedoColor = new(0, 0, 0, 0); // make invisible in real world
            // }

            AddChild(recursedPortal);

            recursedPortal.GlobalTransform = PortalRecusionHelper.PortalImage(exitPortal, this, i);

            passPortals.Add(recursedPortal);

            // recursedPortal.Layers = 0;
            // recursedPortal.SetLayerMaskValue(WritePassLayer, true);
        }
    }












    // helper functions
    
    public Transform3D RealToExitTransform(Transform3D real, MeshInstance3D exitPortal)
    {
        Transform3D local = GlobalTransform.AffineInverse() * real; // Convert from global space to local space at the entrance (this) portal
        Transform3D unscaled = local.Scaled(GlobalBasis.Scale); // Compensate for any scale the entrance portal may have
        Transform3D flipped = unscaled.Rotated(Vector3.Up, (float)Math.PI); // Flip it (the portal always flips the view 180 degrees)

        Vector3 exitScaleVector = exitPortal.GlobalBasis.Scale; // Apply any scale the exit portal may have (and apply custom exit scale)
        Transform3D scaledAtExit = flipped.Scaled((Vector3.One / exitScaleVector) * exitScale);

        Transform3D localAtExit = exitPortal.GlobalTransform * scaledAtExit; // Convert from local space at the exit portal to global space
        return localAtExit;
    }

    // effectively the same logic as RealToExitTransform
    private Vector3 RealToExitPosition(Vector3 real)
    {
        Vector3 local = GlobalTransform.AffineInverse() * real;
        Vector3 unscaled = local * GlobalBasis.Scale; 

        Vector3 exitScaleVector = new Vector3(-1, 1, 1) * exitPortal.GlobalBasis.Scale;
        Vector3 scaledAtExit = (unscaled / exitScaleVector) * exitScale;

        Vector3 localAtExit = exitPortal.GlobalTransform * scaledAtExit;
        return localAtExit;
    }

    public Vector3 RealToExitDirection(Vector3 real)
    {
        Vector3 local = GlobalBasis.Inverse() * real;
        Vector3 unscaled = local * GlobalBasis.Scale;
        Vector3 flipped = unscaled.Rotated(Vector3.Up, (float)Math.PI);

        Vector3 exitScaleVector = exitPortal.GlobalBasis.Scale;
        Vector3 scaledAtExit = (flipped / exitScaleVector) * exitScale;

        Vector3 localAtExit = exitPortal.GlobalBasis * scaledAtExit;
        return localAtExit;
    }










    // Raycasting
    // Raycast against portals (See instructions).

    public static void RayCast(
        SceneTree sceneTree,
        Vector3 from,
        Vector3 direction,
        Callable handleRaycast,
        float maxDistance = float.PositiveInfinity,
        int maxRecursions = 16,
        bool ignoreBackside = true)
    {
        throw new NotImplementedException();
    }
}