using System;
using Liberty.Sdk;
using LibertyFramework.Arsenal.Logic;
using LibertyFramework.Engine;
using LibertyFramework.Engine.Services;

namespace LibertyFramework.Arsenal.Ui
{
    // S-3 on the engine's choreography: Niko uses the trunk with GTA IV's own animations.
    //   turn to the trunk -> amb@car_stash "open_boot" (the lid opens part way in) -> "idle" held while the wheel is
    //   open, "boot_withdraw" for every take/store -> car_boot "close_boot" (the lid shuts part way in). Timings: arsenal.json
    // "trunkTimings" (TrunkTimings). Each step ends when its clip stops or at a timeout, so a clip that fails to load never traps the player; the
    // choreography's cancel path (and the engine, if Arsenal stops) shuts the lid and clears the task.
    internal sealed class TrunkSequence
    {
        private static readonly AnimClip OpenBoot = new AnimClip("amb@car_stash", "open_boot");
        private static readonly AnimClip Idle = new AnimClip("amb@car_stash", "idle");
        private static readonly AnimClip Withdraw = new AnimClip("amb@car_stash", "boot_withdraw");
        private static readonly AnimClip CloseBoot = new AnimClip("car_boot", "close_boot");

        private readonly LibertyModule owner;
        private IChoreography sequence;
        private VehicleRef vehicle;
        private bool closeRequested, handleRequested, browsing;
        private AnimClip current;
        private int currentStarted;
        private TrunkTimings timings;

        internal TrunkSequence(LibertyModule owner) { this.owner = owner; }

        internal bool Active { get { return sequence != null && sequence.IsRunning; } }
        internal bool WheelReady { get { return Active && browsing && !closeRequested; } }

        internal void Begin(PedRef ped, VehicleRef trunk, Vec3 lookAt, TrunkTimings timings)
        {
            LibertyEngine engine = LibertyEngine.Current;
            vehicle = trunk;
            this.timings = timings;
            closeRequested = handleRequested = browsing = false;
            sequence = engine.Animation.Choreography(owner, "trunk")
                .Do(() => engine.Tasks.Clear(ped))
                .TurnTo(ped, lookAt, timings.TurnMilliseconds)
                .At(timings.LidOpenAtMilliseconds, () => engine.Vehicles.OpenDoor(trunk, VehicleDoor.Trunk))
                .Play(ped, OpenBoot, AnimOptions.Default, timings.OpenMinMilliseconds, timings.OpenMaxMilliseconds)
                .Do(() => { browsing = true; Start(engine, ped, Idle); })
                .LoopUntil(() => closeRequested, body => body
                    .WaitUntil(() => closeRequested || handleRequested || StepDone(engine, ped), timings.BrowseStepTimeoutMilliseconds)
                    .Do(() => Next(engine, ped)))
                .Do(() => browsing = false)
                .At(timings.LidCloseAtMilliseconds, () => engine.Vehicles.CloseDoor(trunk, VehicleDoor.Trunk))
                .Play(ped, CloseBoot, AnimOptions.Default, timings.CloseMinMilliseconds, timings.CloseMaxMilliseconds)
                .OnComplete(() => engine.Tasks.Clear(ped))
                .OnCancel(() =>
                {
                    if (engine.Vehicles.Exists(trunk)) { engine.Vehicles.CloseDoor(trunk, VehicleDoor.Trunk); }
                    if (engine.Peds.Exists(ped)) { engine.Tasks.Clear(ped); }
                })
                .Begin();
        }

        // A take or store just happened: reach into the trunk.
        internal void Handle() { if (WheelReady) { handleRequested = true; } }

        internal void Close() { closeRequested = true; }

        // Error/storage-closed path: stop now (the cancel step shuts the lid).
        internal void Abort()
        {
            if (sequence != null && sequence.IsRunning) { sequence.Cancel(); }
            sequence = null;
        }

        // Engine tick: the choreography watches the ped; the vehicle is watched here.
        internal void Update()
        {
            if (Active && !LibertyEngine.Current.Vehicles.Exists(vehicle)) { sequence.Cancel(); }
        }

        private void Start(LibertyEngine engine, PedRef ped, AnimClip clip)
        {
            engine.Animation.Play(ped, clip, AnimOptions.Default);
            current = clip;
            currentStarted = Environment.TickCount;
        }

        private bool StepDone(LibertyEngine engine, PedRef ped)
        {
            int elapsed = unchecked(Environment.TickCount - currentStarted);
            if (current.Name == Withdraw.Name) { return elapsed >= timings.WithdrawMaxMilliseconds || (elapsed >= timings.WithdrawMinMilliseconds && !engine.Animation.IsPlaying(ped, Withdraw)); }
            return elapsed >= timings.IdleMinMilliseconds && !engine.Animation.IsPlaying(ped, Idle);
        }

        private void Next(LibertyEngine engine, PedRef ped)
        {
            if (closeRequested) { return; }
            if (handleRequested) { handleRequested = false; Start(engine, ped, Withdraw); return; }
            if (StepDone(engine, ped)) { Start(engine, ped, Idle); }
        }
    }
}
