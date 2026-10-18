// Stick & Puck: a pixel-art hockey game drawn in 3D. Everything renders to a 480x270 target
// that is scaled up by whole pixels, so the window size never changes the look.
// OpenGL picks its graphics card when the window is made, so a game that should run on another
// card than it started on starts itself again on that card first (see GpuLaunch).
StickAndPuck.App.Core.AssetRoot.Enter();
if (!OperatingSystem.IsBrowser() && !StickAndPuck.App.GpuLaunch.OntoWanted())
{
    StickAndPuck.App.Core.Game.Run();
    if (!OperatingSystem.IsBrowser())
    {
        StickAndPuck.App.GpuLaunch.AfterRun();
    }
}
