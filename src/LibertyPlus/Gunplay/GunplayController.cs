using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using GTA;
using LibertyFramework.Arsenal.Contracts;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Input;
using LibertyFramework.Core.Logging;
using LibertyFramework.Core.Math3;
using LibertyFramework.Core.Memory;
using LibertyFramework.Core.Performance.Logic;
using LibertyFramework.GameApi;
using LibertyFramework.Gunplay.Aim;
using LibertyFramework.Gunplay.Crosshair;
using LibertyFramework.Gunplay.Logic;
using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Gunplay.Recoil;
using LibertyFramework.Gunplay.Spread;
using LibertyFramework.Weapons;
using LibertyFramework.Weapons.Logic;

namespace LibertyFramework.Gunplay
{
    // Per-frame gunplay loop. Order each frame:
    //   shots (clip delta) -> audit last frame's real bullets -> spread model -> write accuracy
    //   -> recoil step -> aim-camera write -> reticle hide / crosshair draw.
    // Only registered test weapons (config weapons 58+) and the Stage 1 catalog weapons (T-041, Stage1Gate) get spread/recoil; free aim and the
    // crosshair apply universally while their toggles are on. Every engine write is undone on unload.
    [global::Liberty.Sdk.Module("gunplay", Order = 10, Capabilities = new[] { global::Liberty.Sdk.Capabilities.EngineInternal }, Description = "Gunplay: free aim, recoil, spread, crosshair")]
    public sealed class GunplayController : LibertyFramework.Engine.Module
    {
        private const int ConfigPollMilliseconds = 1000;
        private const int StateLogMilliseconds = 5000;
        private const int DetailedAuditBullets = 40;
        private const int TimingReportMilliseconds = 30000;

        internal static GunplayController Instance { get; private set; }

        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly TickTimings tickTimings = new TickTimings();
        private readonly GunplayPhaseTimings phaseTimings = new GunplayPhaseTimings();
        private int lastTimingReportTicks;
        private readonly GunplayConfigStore store;
        // T-041: the weapon catalog decides which vanilla-id weapons the Liberty profiles apply to (Stage1Gate).
        // T-043 reticle: the style of the held weapon (resolved on the tick, drawn from it), how visible it is, and the truthfulness log.
        private ResolvedReticle activeReticle;
        private double reticleOpacity = 1.0;
        private readonly ReticleTruth reticleTruth = new ReticleTruth();
        private bool reticleDebug;
        private double lastReticleLogMilliseconds = double.NegativeInfinity;
        private int reticleWeaponId = -1;
        private GunplayConfig reticleConfig;
        private WeaponCatalog reticleCatalog;
        // Test hooks (`aim` command): pretend the aim button is held and force stance/speed, so the autopilot can check the model and reticle.
        private bool testAiming;
        private bool testCrouched;
        private bool testCover;
        private double? testSpeed;
        // T-042 range recording: bullet deviation from an explicit aim point (`range` command), not from the camera.
        private Vec3? rangeAim;
        private readonly RangeRecorder rangeRecorder = new RangeRecorder();
        private double rangeStartedMilliseconds;
        private WeaponCatalog weaponCatalog;
        private string weaponCatalogHash;
        private int gateWeaponId = -1;
        private GunplayConfig gateConfig;
        private WeaponCatalog gateCatalog;
        private WeaponProfile gateProfile;
        private readonly ControllerInput controller = new ControllerInput();
        private readonly RecoilSolver recoil = new RecoilSolver(Environment.TickCount);
        private readonly SpreadModel spread = new SpreadModel();
        private readonly SpreadCalibrator calibrator = new SpreadCalibrator();
        private readonly CrosshairRenderer crosshair = new CrosshairRenderer();
        private readonly Dictionary<int, ShotAuditStats> audits = new Dictionary<int, ShotAuditStats>();
        private readonly DebugHitTracker debugHit = new DebugHitTracker();
        private readonly Random feelRandom = new Random();

        private bool initialized;
        private bool disabled;
        private string disabledReason = "";
        private double lastTickMilliseconds;
        private double lastConfigPollMilliseconds;
        private double lastStateLogMilliseconds;
        private int lastWeaponId = -1;
        private int lastClip = -1;
        private double lastShotMilliseconds = double.NegativeInfinity;
        private bool realRecoilPresent;

        private GameAddresses addresses;
        private LiveMemory memory;
        private WeaponInfoTable weaponInfo;
        private AimCamera onFootCamera;
        private AimCamera vehicleCamera;
        private AimCamera aimCamera;
        private HudReticle hud;
        private GamePrefs prefs;
        private PlayerMemory playerMemory;
        private BulletLog bullets;
        private FreeAimMode freeAim;

        // Frame snapshot used for drawing and DevTools inspection.
        private ShooterState state;
        private WeaponProfile activeProfile;
        private int activeWeaponId;
        private double currentConeDegrees;
        private double displayConeDegrees;
        private double writtenConeDegrees;
        private double writtenAccuracy;
        private bool aimCameraActive;
        private bool aiming;
        private double lastFov = 45;
        // T-026 camera caching (gunplay.json "performance").
        private int cachedGameCamera;
        private bool nativeCostLogged;
        private bool directVerified;
        private EngineThreadProbe threadProbe;
        private bool gameCameraValid;
        private double lastGameCameraReadMilliseconds = double.NegativeInfinity;
        private double lastFovReadMilliseconds = double.NegativeInfinity;
        private bool drawCrosshair;
        private int playerIndex;
        private uint playerPed;
        private int lastAppliedShots;
        private double lastAttachmentBloomMultiplier = 1.0;
        private bool loggedOwnerMismatch;
        private bool loggedCameraCrossCheck;
        private bool loggedCameraCrossCheckDeferred;
        private bool useShdnDirection;
        private double pixelsPerTangent;
        private bool loggedProjection;
        private double projectionFov = double.NaN;
        private int projectionHeight;
        private Size screenResolution;
        private double lastResolutionReadMilliseconds;
        private bool drawFailed;
        private ShoulderSwap shoulderSwap;
        private AimCameraSettings shoulderSettings;
        private bool feelDisabled;
        private GTA.Camera feelCamera;
        private int feelCameraHandle;
        private float originalFov;
        private bool fovApplied;
        private bool fovRecovered;
        private readonly string fovStatePath = Path.Combine(LibertyPaths.StateDirectory, "feel_fov_restore.json");
        private bool debugHitDisabled;
        private double lastDebugHitSampleMilliseconds = double.NegativeInfinity;
        private bool aimingSwitchDisabled;
        private double lastFireMilliseconds = double.NegativeInfinity;
        private double fireIntervalMilliseconds;
        private string configuredAimProfile;
        private bool configuredAimStartup;

        internal bool FreeAimEnabled;
        internal bool CrosshairEnabled = true;
        internal bool CameraKickEnabled = true;
        internal bool SpreadControlEnabled = true;
        internal bool InfiniteAmmo;
        // T-026: Environment.TickCount of the player's last detected shot (any weapon with a clip); 0 = none yet.
        // CombatEffects samples damage at full rate only shortly after a shot.
        internal static volatile int LastShotTickCount;
        internal bool DebugOverlay;

        public GunplayController()
        {
            Instance = this;
            Interval = 0;
            store = new GunplayConfigStore(LibertyPaths.GunplayConfig, RuntimeLog.Info, RuntimeLog.Error);
            store.Poll(true);
            FreeAimEnabled = store.Active != null && store.Active.FreeAim.Profile == "free" && store.Active.FreeAim.EnabledOnStartup;
            configuredAimProfile = store.Active != null ? store.Active.FreeAim.Profile : null;
            configuredAimStartup = store.Active != null && store.Active.FreeAim.EnabledOnStartup;
            Tick += OnTick;
            PerFrameDrawing += OnDraw;
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
            // Game exit may skip DomainUnload; restoring on ProcessExit too keeps the menu Auto-Aim value intact.
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            RuntimeLog.Info("gunplay_started config=" + (store.Active != null ? "ok" : "missing") + " free_aim_on_start=" + FreeAimEnabled);
        }

        internal GunplayConfig Config { get { return store.Active; } }

        // T-041: whether the spread model can drive this weapon's accuracy (its WeaponInfo entry was proven against WeaponInfo.xml).
        internal bool CanControlSpread(int weaponId) { return weaponInfo != null && weaponInfo.CanWrite(weaponId); }
        internal GunplayConfigStore Store { get { return store; } }
        internal SpreadCalibrator Calibrator { get { return calibrator; } }
        internal bool Initialized { get { return initialized; } }
        internal bool Disabled { get { return disabled; } }
        internal GameAddresses Addresses { get { return addresses; } }
        internal FreeAimMode FreeAim { get { return freeAim; } }

        private double Now { get { return clock.Elapsed.TotalMilliseconds; } }

        // T-026: every tick's wall-clock cost goes to the shared CostMeter report.
        private void OnTick(object sender, EventArgs args)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            EngineThreadProbe probe = threadProbe;
            int frameAtStart = probe != null ? probe.FrameCount : 0;
            try { TickBody(sender, args); }
            finally
            {
                LibertyFramework.Core.Performance.Logic.CostMeter.Add("tick.gunplay", started);
                if (probe != null) { probe.ObserveTick(frameAtStart); }
            }
        }

        private void TickBody(object sender, EventArgs args)
        {
            if (disabled) { return; }
            long tickStart = Stopwatch.GetTimestamp();
            long setupEnd = 0;
            long cameraEnd = 0;
            long bulletsEnd = 0;
            long weaponEnd = 0;
            try
            {
                double now = Now;
                double deltaSeconds = (now - lastTickMilliseconds) / 1000.0;
                lastTickMilliseconds = now;
                GunplayConfig config = store.Active;
                if (config == null) { store.Poll(false); return; }
                deltaSeconds = Math.Max(0, Math.Min(config.RecoilGlobal.MaximumDeltaSeconds, deltaSeconds));
                // Screen size from the window (ScreenInfo), cached for the draw path; Game.Resolution stalls or deadlocks the game.
                if (screenResolution.Height == 0 || now - lastResolutionReadMilliseconds >= 1000)
                {
                    screenResolution = LibertyFramework.Engine.Services.ScreenInfo.Size;
                    lastResolutionReadMilliseconds = now;
                }
                if (now - lastConfigPollMilliseconds >= ConfigPollMilliseconds)
                {
                    lastConfigPollMilliseconds = now;
                    store.Poll(false);
                    config = store.Active;
                    PollCatalog(config);
                }
                if (config.FreeAim.Profile != configuredAimProfile || config.FreeAim.EnabledOnStartup != configuredAimStartup)
                {
                    configuredAimProfile = config.FreeAim.Profile;
                    configuredAimStartup = config.FreeAim.EnabledOnStartup;
                    FreeAimEnabled = config.FreeAim.Profile == "free" && config.FreeAim.EnabledOnStartup;
                }

                long probe = Stopwatch.GetTimestamp();
                Player player = Player;
                if (player == null || player.Character == null || !Natives.IsPlayerPlaying(player))
                {
                    drawCrosshair = false;
                    RestoreFeel();
                    return;
                }
                if (!fovRecovered)
                {
                    try { RecoverFeel(); }
                    catch (Exception error) { feelDisabled = true; RuntimeLog.Error("feel_fov_recovery_failed error=" + error); }
                    fovRecovered = true;
                }
                if (!initialized) { Initialize(config); }
                if (!nativeCostLogged) { nativeCostLogged = true; LogNativeCost(player); }
                if (!directVerified)
                {
                    directVerified = true;
                    try { Natives.VerifyDirect(player, player.Character, Natives.GameCamHandle()); RuntimeLog.Info(DirectNatives.Summary()); }
                    catch (Exception error) { RuntimeLog.Error("direct_natives_verify_failed error=" + error.Message); }
                }
                CostMeter.Add("gp.player", probe); probe = Stopwatch.GetTimestamp();
                playerIndex = Natives.PlayerIndex();
                playerPed = playerMemory != null ? playerMemory.PedPointer(playerIndex) : 0;
                controller.Poll();
                CostMeter.Add("gp.index_pad", probe); probe = Stopwatch.GetTimestamp();
                if (!aimingSwitchDisabled)
                {
                    try { CycleWhileAiming(player.Character, config); }
                    catch (Exception error) { aimingSwitchDisabled = true; RuntimeLog.Error("feature_disabled switch_while_aiming error=" + error); }
                }

                CostMeter.Add("gp.cycle", probe); probe = Stopwatch.GetTimestamp();
                try { UpdateFreeAim(player, config); }
                catch (Exception error)
                {
                    RuntimeLog.Error("feature_disabled free_aim error=" + error);
                    try { if (freeAim != null) { freeAim.Disable(player); } }
                    catch (Exception restoreError) { RuntimeLog.Error("restore_freeaim_failed error=" + restoreError.Message); }
                    freeAim = null;
                }
                CostMeter.Add("gp.freeaim", probe); probe = Stopwatch.GetTimestamp();
                Ped ped = player.Character;
                int weaponId = Natives.CurrentWeapon(ped);
                if (weaponId != lastWeaponId)
                {
                    OnWeaponChanged(weaponId, config);
                    lastAttachmentBloomMultiplier = 1.0;
                }
                activeWeaponId = weaponId;
                activeProfile = ResolveProfile(config, weaponId);

                CostMeter.Add("gp.weapon_id", probe); probe = Stopwatch.GetTimestamp();
                SampleState(ped);
                CostMeter.Add("gp.state", probe); probe = Stopwatch.GetTimestamp();
                int shots = DetectShots(ped, weaponId);
                CostMeter.Add("gp.shots", probe);
                if (DebugOverlay && shots > 0)
                {
                    if (lastFireMilliseconds > 0) { fireIntervalMilliseconds = (now - lastFireMilliseconds) / shots; }
                    lastFireMilliseconds = now;
                    debugHit.NoteShot(now);
                }
                if (activeProfile != null && shots > 0)
                {
                    double recoilMultiplier = RecoilMultiplier.For(activeProfile.Recoil, config.Movement, state);
                    ICarriedWeaponsSource attachmentSource = ArsenalRegistry.CarriedWeapons;
                    lastAttachmentBloomMultiplier = attachmentSource != null ? attachmentSource.PerShotBloomMultiplier(weaponId) : 1.0;
                    for (int shot = 0; shot < shots; shot++)
                    {
                        recoil.AddShot(activeProfile.Recoil, now, recoilMultiplier);
                        spread.AddShot(activeProfile.Spread, now, lastAttachmentBloomMultiplier);
                    }
                    lastShotMilliseconds = now;
                    lastAppliedShots += shots;
                }

                setupEnd = Stopwatch.GetTimestamp();
                long mark = setupEnd;
                int gameCamera = CurrentGameCamera(config, now);
                CostMeter.Add("cam.handle", mark);
                uint aimCam = 0;
                // Drive-by uses the vehicle follow camera; on foot the third-person aim camera.
                aimCamera = state.InVehicle ? vehicleCamera : onFootCamera;
                if (aimCamera != null && gameCameraValid)
                {
                    mark = Stopwatch.GetTimestamp();
                    try { aimCam = aimCamera.FindActive(gameCamera); }
                    catch (Exception error) { DisableCamera(error); }
                    CostMeter.Add("cam.find_active", mark);
                }
                aimCameraActive = aimCam != 0;
                mark = Stopwatch.GetTimestamp();
                aiming = (aimCameraActive && !state.InVehicle) || Game.isGameKeyPressed(GameKey.Aim) || testAiming;
                CostMeter.Add("cam.aim_key", mark);
                state.Aiming = aiming;
                if (gameCameraValid)
                {
                    // FOV only matters for the aim crosshair; between aims it is refreshed at a slow cadence.
                    double fovRefresh = config.Performance != null ? config.Performance.FovRefreshMilliseconds : 0;
                    if (aiming || now - lastFovReadMilliseconds >= fovRefresh)
                    {
                        mark = Stopwatch.GetTimestamp();
                        lastFov = Natives.CamFov(gameCamera);
                        lastFovReadMilliseconds = now;
                        CostMeter.Add("cam.fov", mark);
                    }
                    // Pixel scale depends on FOV and viewport, not camera position. Avoid two
                    // projection natives every frame while the aim camera is steady.
                    if (aiming && (double.IsNaN(projectionFov) || Math.Abs(lastFov - projectionFov) > 0.25 ||
                        screenResolution.Height != projectionHeight))
                    {
                        mark = Stopwatch.GetTimestamp();
                        MeasureProjection(gameCamera);
                        CostMeter.Add("cam.projection", mark);
                    }
                }

                cameraEnd = Stopwatch.GetTimestamp();
                // Each engine feature fails independently: an exception disables only that feature.
                if (bullets != null)
                {
                    try { AuditBullets(config, gameCamera, weaponId, now); }
                    catch (Exception error) { bullets = null; RuntimeLog.Error("feature_disabled shot_audit error=" + error); }
                }
                bulletsEnd = Stopwatch.GetTimestamp();
                probe = Stopwatch.GetTimestamp();
                try { UpdateSpread(config, now, deltaSeconds); }
                catch (Exception error) { DisableSpread(error); }
                CostMeter.Add("gp.spread", probe); probe = Stopwatch.GetTimestamp();
                try { UpdateRecoil(config, now, deltaSeconds, aimCam, gameCamera); }
                catch (Exception error) { DisableCamera(error); }
                CostMeter.Add("gp.recoil", probe); probe = Stopwatch.GetTimestamp();
                if (shoulderSwap != null)
                {
                    try
                    {
                        shoulderSwap.Update(config.ShoulderSwap, controller, aiming,
                            LibertyFramework.DevTools.DevToolsMenu.IsOpen || Natives.IsPauseMenuActive(), deltaSeconds);
                    }
                    catch (Exception error)
                    {
                        RuntimeLog.Error("feature_disabled shoulder_swap error=" + error);
                        try { shoulderSwap.Restore(); } catch (Exception restoreError) { RuntimeLog.Error("restore_shoulder_failed error=" + restoreError.Message); }
                        shoulderSwap = null;
                    }
                }
                CostMeter.Add("gp.shoulder", probe); probe = Stopwatch.GetTimestamp();
                try { UpdateFeel(config, shots, deltaSeconds, aimCam, gameCamera); }
                catch (Exception error)
                {
                    RuntimeLog.Error("feature_disabled feel error=" + error);
                    feelDisabled = true;
                    try { RestoreFeel(); }
                    catch (Exception restoreError) { RuntimeLog.Error("restore_feel_failed error=" + restoreError); }
                }
                CostMeter.Add("gp.feel", probe);
                weaponEnd = Stopwatch.GetTimestamp();
                if (DebugOverlay && !debugHitDisabled && now - lastDebugHitSampleMilliseconds >= config.DebugHit.ScanIntervalMilliseconds)
                {
                    try { debugHit.Sample(ped, config.DebugHit.ScanRadiusMeters, now, config.DebugHit.WorldClassificationDelayMilliseconds); lastDebugHitSampleMilliseconds = now; }
                    catch (Exception error) { debugHitDisabled = true; debugHit.Reset(); RuntimeLog.Error("feature_disabled debug_hit error=" + error); }
                }
                else if (!DebugOverlay) { debugHit.Reset(); }
                try { UpdateReticle(config, ped); }
                catch (Exception error) { DisableHud(error); }

                if (InfiniteAmmo) { TestWeaponActions.KeepReserve(player, config.TestRange.AmmoRefillRounds); }
                if (now - lastStateLogMilliseconds >= StateLogMilliseconds && activeProfile != null)
                {
                    lastStateLogMilliseconds = now;
                    RuntimeLog.Info("gunplay_state weapon=" + weaponId + " profile=" + activeProfile.ProfileName + " state=" + state.Describe() +
                        " cone=" + currentConeDegrees.ToString("0.00") + " accuracy=" + writtenAccuracy.ToString("0.000") + " gain=" + calibrator.Gain.ToString("0.000") +
                        " aimcam=" + aimCameraActive + " kick_ready=" + KickReady(config) + " shots=" + lastAppliedShots +
                        " attachment_bloom=" + lastAttachmentBloomMultiplier.ToString("0.00"));
                }
            }
            catch (Exception error)
            {
                DisableAfterError(error);
            }
            finally
            {
                long tickEnd = Stopwatch.GetTimestamp();
                tickTimings.Observe(tickStart, tickEnd);
                if (weaponEnd != 0) { phaseTimings.Observe(tickStart, setupEnd, cameraEnd, bulletsEnd, weaponEnd, tickEnd); }
                int now = Environment.TickCount;
                if (lastTimingReportTicks == 0) { lastTimingReportTicks = now; }
                else if (unchecked(now - lastTimingReportTicks) >= TimingReportMilliseconds)
                {
                    lastTimingReportTicks = now;
                    RuntimeLog.Info("performance " + tickTimings.ReportAndReset() + " " + phaseTimings.ReportAndReset());
                    RuntimeLog.Info("performance_scripts " + CostMeter.ReportAndReset(CostReader.Log));
                    if (threadProbe != null)
                    {
                        RuntimeLog.Info(threadProbe.ReportAndReset());
                        try
                        {
                            Player probePlayer = Player;
                            Ped probePed = probePlayer != null ? probePlayer.Character : null;
                            if (probePed != null && probePed.Exists()) { RuntimeLog.Info(threadProbe.ProbeHealth(probePed.GetHashCode(), () => probePed.Health)); }
                        }
                        catch (Exception error) { RuntimeLog.Error("direct_native_probe_failed error=" + error.Message); threadProbe = null; }
                    }
                }
            }
        }

        private void Initialize(GunplayConfig config)
        {
            initialized = true;
            Stopwatch timer = Stopwatch.StartNew();
            PollCatalog(config);
            try
            {
                // ADR-0006: one memory scan per session, owned by the engine (it also maps the direct-native handlers
                // and trusts the permanent pools); gunplay shares its reader and addresses.
                LibertyFramework.Engine.EngineMemory shared = LibertyFramework.Engine.LibertyEngine.Current.Memory;
                if (!shared.Resolve()) { throw new InvalidOperationException("engine memory unresolved"); }
                memory = shared.Live;
                addresses = shared.Addresses;
                if (addresses.FrameCounterGlobal != 0)
                {
                    try { threadProbe = new EngineThreadProbe(addresses.FrameCounterGlobal, addresses.GetCharHealthHandler); }
                    catch (Exception error) { RuntimeLog.Error("engine_thread_probe_unavailable error=" + error.Message); }
                }
                RuntimeLog.Info("gunplay_engine_access elapsed_ms=" + timer.ElapsedMilliseconds);
            }
            catch (Exception error)
            {
                RuntimeLog.Error("engine_resolve_failed all engine features disabled error=" + error);
                return;
            }

            if (addresses.PrefsResolved) { prefs = new GamePrefs(memory, addresses); }
            if (addresses.LockOnResolved) { playerMemory = new PlayerMemory(memory, addresses); }
            if (addresses.HudResolved) { hud = new HudReticle(memory, addresses); }
            if (addresses.BulletsResolved && addresses.LockOnResolved) { bullets = new BulletLog(memory, addresses); }
            if (addresses.AimCameraResolved)
            {
                onFootCamera = new AimCamera(memory, addresses, addresses.AimCamType, addresses.AimCamPitchOffset, addresses.AimCamHeadingOffset, "aim");
                onFootCamera.ResetValidation();
                if (addresses.VehicleCamPitchOffset > 0)
                {
                    vehicleCamera = new AimCamera(memory, addresses, addresses.VehicleCamType, addresses.VehicleCamPitchOffset, addresses.VehicleCamHeadingOffset, "vehicle");
                    vehicleCamera.ResetValidation();
                }
            }
            freeAim = new FreeAimMode(prefs, playerMemory, hud);
            freeAim.RecoverFromPreviousSession();
            if (addresses.AimCamSettingsResolved)
            {
                try { shoulderSettings = new AimCameraSettings(memory, addresses); shoulderSwap = new ShoulderSwap(shoulderSettings); }
                catch (Exception error) { RuntimeLog.Error("feature_disabled shoulder_swap validation error=" + error.Message); }
            }

            if (addresses.WeaponInfoResolved)
            {
                weaponInfo = new WeaponInfoTable(memory, addresses);
                try
                {
                    string xmlPath = WeaponInfoXml.ActivePath(LibertyPaths.GameDirectory);
                    Dictionary<string, float> xml = WeaponInfoXml.ReadAccuracies(xmlPath);
                    Dictionary<int, float> expected = new Dictionary<int, float>();
                    Dictionary<int, float> stage1Expected = new Dictionary<int, float>();
                    foreach (WeaponProfile weapon in config.Weapons)
                    {
                        float accuracy;
                        if (!xml.TryGetValue(weapon.WeaponInfoName, out accuracy)) { continue; }
                        if (weapon.IsTestWeapon) { expected[weapon.WeaponId] = accuracy; } else { stage1Expected[weapon.WeaponId] = accuracy; }
                    }
                    RuntimeLog.Info("weaponinfo_xml path=" + xmlPath + " test_entries=" + expected.Count + " stage1_entries=" + stage1Expected.Count);
                    weaponInfo.Validate(expected);
                    if (stage1Expected.Count > 0) { weaponInfo.ValidateExtra(stage1Expected); }
                }
                catch (Exception error)
                {
                    RuntimeLog.Error("weaponinfo_validation_error spread writes disabled error=" + error.Message);
                }
            }

            string scripts = Path.Combine(LibertyPaths.GameDirectory, "scripts");
            realRecoilPresent = File.Exists(Path.Combine(scripts, "WeaponRecoil.net.dll"));
            if (realRecoilPresent)
            {
                RuntimeLog.Error("real_recoil_detected scripts/WeaponRecoil.net.dll camera_kick_for_test_weapons=" + config.RecoilGlobal.AllowWithRealRecoil +
                    " (both mods kicking would double the recoil)");
            }
            RuntimeLog.Info("gunplay_initialized prefs=" + (prefs != null) + " lockon=" + (playerMemory != null) + " hud=" + (hud != null) +
                " aimcam=" + (onFootCamera != null) + " vehiclecam=" + (vehicleCamera != null) + " aim_settle=" + addresses.AimSettleResolved + " weaponinfo=" + (weaponInfo != null && weaponInfo.Validated) + " bullets=" + (bullets != null) +
                " elapsed_ms=" + timer.ElapsedMilliseconds);
        }

        private void UpdateFreeAim(Player player, GunplayConfig config)
        {
            if (freeAim == null) { return; }
            bool enabled = config.FreeAim.Profile == "free" && FreeAimEnabled;
            if (enabled && !freeAim.Active) { freeAim.Enable(playerIndex); }
            else if (!enabled && freeAim.Active) { freeAim.Disable(player); }
            freeAim.Update(player, playerIndex, config.FreeAim);
        }

        // The Liberty profile of a held weapon after the Stage 1 gate, or null (vanilla). Cached per weapon, config and catalog.
        private WeaponProfile ResolveProfile(GunplayConfig config, int weaponId)
        {
            if (weaponId != gateWeaponId || !ReferenceEquals(config, gateConfig) || !ReferenceEquals(weaponCatalog, gateCatalog))
            {
                gateProfile = Stage1Gate.ProfileFor(config, weaponCatalog, weaponId);
                gateWeaponId = weaponId;
                gateConfig = config;
                gateCatalog = weaponCatalog;
            }
            return gateProfile;
        }

        // weapon-catalog.json is read again when it changes; a broken file keeps the last good catalog.
        private void PollCatalog(GunplayConfig config)
        {
            try
            {
                if (!File.Exists(LibertyPaths.WeaponCatalog)) { return; }
                byte[] bytes = JsonStore.ReadBytes(LibertyPaths.WeaponCatalog);
                string hash = JsonStore.Hash(bytes);
                if (hash == weaponCatalogHash) { return; }
                weaponCatalogHash = hash;
                WeaponCatalog candidate = JsonStore.Parse<WeaponCatalog>(bytes);
                candidate.Validate();
                weaponCatalog = candidate;
                LogStage1Gate(config);
            }
            catch (Exception error) { RuntimeLog.Error("weapon_catalog_rejected error=" + error.Message + " (gunplay keeps the last valid catalog)"); }
        }

        // One line per catalog weapon: whether the gunplay gate lets its Liberty profile apply (evidence for T-041).
        private void LogStage1Gate(GunplayConfig config)
        {
            if (weaponCatalog == null || config == null) { return; }
            foreach (WeaponCatalogEntry entry in weaponCatalog.Entries)
            {
                if (entry.WeaponId >= 58 && !entry.Stage1) { continue; }
                WeaponProfile profile = config.FindWeapon(entry.WeaponId);
                bool allowed = Stage1Gate.Allows(config, weaponCatalog, profile);
                RuntimeLog.Info("stage1_gate id=" + entry.Id + " weapon=" + entry.WeaponId + " class=" + (entry.WeaponClass ?? "-") + " tier=" + (entry.Tier ?? "-") +
                    " stage1=" + entry.Stage1 + " profile=" + (profile != null ? profile.ProfileName : "none") + " gunplay=" + (allowed ? "liberty" : "vanilla"));
            }
        }

        private void OnWeaponChanged(int weaponId, GunplayConfig config)
        {
            WeaponProfile previous = ResolveProfile(config, lastWeaponId);
            // A catalog weapon shares its WeaponInfo entry with every NPC carrying it: give the game its own accuracy back.
            if (previous != null && !previous.IsTestWeapon && weaponInfo != null) { weaponInfo.Restore(lastWeaponId); }
            WeaponProfile now = ResolveProfile(config, weaponId);
            RuntimeLog.Info("weapon_changed from=" + lastWeaponId + " to=" + weaponId + " profile=" + (now != null ? now.ProfileName : "vanilla") +
                (now != null && !now.IsTestWeapon ? " catalog=" + now.CatalogId : ""));
            lastWeaponId = weaponId;
            lastClip = -1;
            recoil.Reset();
            spread.Reset();
            crosshair.Reset();
            lastFireMilliseconds = double.NegativeInfinity;
            fireIntervalMilliseconds = 0;
        }

        private void CycleWhileAiming(Ped ped, GunplayConfig config)
        {
            if (!config.SwitchWhileAiming.Enabled || LibertyFramework.DevTools.DevToolsMenu.IsOpen ||
                !Game.isGameKeyPressed(GameKey.Aim) || Natives.IsInAnyCar(ped)) { return; }
            int direction = controller.WasPressed(config.SwitchWhileAiming.NextButton == "DPadRight" ? ControllerInput.DPadRight : ControllerInput.DPadLeft) ? 1 :
                controller.WasPressed(config.SwitchWhileAiming.PreviousButton == "DPadRight" ? ControllerInput.DPadRight : ControllerInput.DPadLeft) ? -1 : 0;
            if (direction == 0) { return; }
            ICarriedWeaponsSource source = ArsenalRegistry.CarriedWeapons;
            if (source == null || source.Carried == null) { return; }
            List<int> candidates = new List<int>();
            foreach (CarriedWeapon weapon in source.Carried)
            {
                if (weapon == null || weapon.WeaponId <= 0 || candidates.Contains(weapon.WeaponId)) { continue; }
                if (ped.Weapons.FromType((Weapon)weapon.WeaponId).isPresent) { candidates.Add(weapon.WeaponId); }
            }
            if (candidates.Count < 2) { return; }
            int current = (int)ped.Weapons.CurrentType;
            int selected = AimingCycleRules.NextWeapon(candidates, current, direction);
            ped.Weapons.Select((Weapon)selected);
            RuntimeLog.Info("aiming_cycle from=" + current + " to=" + selected + " count=" + candidates.Count);
        }

        private void SampleState(Ped ped)
        {
            state = new ShooterState();
            state.InVehicle = Natives.IsInAnyCar(ped);
            state.SpeedMetersPerSecond = state.InVehicle ? 0 : Natives.CharSpeed(ped);
            state.Crouched = !state.InVehicle && Natives.IsDucking(ped);
            state.InCover = !state.InVehicle && Natives.IsInCover(ped);
            state.Airborne = !state.InVehicle && Natives.IsInAir(ped);
            state.Aiming = aiming;
            if (testCrouched) { state.Crouched = true; }
            if (testCover) { state.InCover = true; }
            if (testSpeed.HasValue) { state.SpeedMetersPerSecond = testSpeed.Value; }
        }

        private int DetectShots(Ped ped, int weaponId)
        {
            // T-026: direct clip reads (ammo/max of the current weapon) when verified; SHDN's Weapon object otherwise.
            int clip = weaponId > 0 ? Natives.AmmoInClip(ped, weaponId) : -1;
            int maximum = clip >= 0 ? Natives.MaxAmmoInClip(ped, weaponId) : -1;
            if (clip < 0 || maximum < 0)
            {
                GTA.value.Weapon current = ped.Weapons.Current;
                if (current == null) { lastClip = -1; return 0; }
                clip = current.AmmoInClip;
                maximum = current.MaxAmmoInClip;
            }
            int shots = lastClip >= 0 && clip < lastClip ? lastClip - clip : 0;
            lastClip = clip;
            if (shots > 0) { LastShotTickCount = Environment.TickCount | 1; }
            // A clip drop larger than a magazine means a scripted ammo change, not firing.
            return shots > 0 && shots <= Math.Max(1, maximum) ? Math.Min(shots, 8) : 0;
        }

        // Screen pixels per unit tangent at the screen centre, measured by projecting two points through the
        // game viewport: one on the camera ray and one 2 degrees above it. This ties the crosshair gap to the
        // game's real projection instead of an assumed FOV axis.
        private void MeasureProjection(int gameCamera)
        {
            const double probeDegrees = 2.0;
            const double probeDistance = 25.0;
            try
            {
                Vec3 position = Natives.CamPosition(gameCamera);
                Vec3 rotation = Natives.CamRotation(gameCamera);
                Vec3 forward = Vec3.FromPitchHeadingDegrees(rotation.X, rotation.Z);
                Vec3 raised = Vec3.FromPitchHeadingDegrees(rotation.X + probeDegrees, rotation.Z);
                int viewport = Natives.GameViewportId();
                float centerX, centerY, upX, upY;
                if (!Natives.ViewportPositionOfCoord(viewport, position + forward * probeDistance, out centerX, out centerY) ||
                    !Natives.ViewportPositionOfCoord(viewport, position + raised * probeDistance, out upX, out upY)) { return; }
                double pixels = Math.Abs(upY - centerY) * screenResolution.Height;
                double measured = pixels / Math.Tan(probeDegrees * Math.PI / 180.0);
                if (measured <= 1 || double.IsNaN(measured)) { return; }
                pixelsPerTangent = measured;
                projectionFov = lastFov;
                projectionHeight = screenResolution.Height;
                if (!loggedProjection)
                {
                    loggedProjection = true;
                    double formulaVertical = CrosshairRenderer.ConeToPixels(probeDegrees, lastFov, true, screenResolution);
                    double formulaHorizontal = CrosshairRenderer.ConeToPixels(probeDegrees, lastFov, false, screenResolution);
                    RuntimeLog.Info("crosshair_projection fov=" + lastFov.ToString("0.0") + " center=" + centerX.ToString("0.000") + "," + centerY.ToString("0.000") +
                        " probe_px=" + pixels.ToString("0.0") + " formula_vertical_px=" + formulaVertical.ToString("0.0") +
                        " formula_horizontal_px=" + formulaHorizontal.ToString("0.0"));
                }
            }
            catch (Exception error)
            {
                pixelsPerTangent = 0;
                RuntimeLog.Error("crosshair_projection_failed using FOV formula error=" + error.Message);
            }
        }

        // ---- T-042 range measurement ----

        protected internal override void OnStart()
        {
            Engine.Commands.Register(this, "aim", "aim on|off [crouched] [cover] [speed <m/s>] - test hook: act as if the aim button is held, with a forced stance/speed (T-043)", AimCommand);
            Engine.Commands.Register(this, "reticle", "reticle debug on|off | check | reset | status - log the drawn opening against the cone and check it (T-043)", ReticleCommand);
            Engine.Commands.Register(this, "swap", "swap left|right|toggle|status|probe - shoulder swap without the aim button; probe checks the live camera table (T-042)", SwapCommand);
            Engine.Commands.Register(this, "range",
                "range start ahead <m> [height m] | start <x> <y> <z> | fire <ms> [mode 1-4] | stop | status - record bullet deviation from an explicit aim point (T-042)", RangeCommand);
        }

        private string AimCommand(string[] args)
        {
            string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            if (verb == "off")
            {
                testAiming = false; testCrouched = false; testCover = false; testSpeed = null;
                RuntimeLog.Info("aim_test off");
                return "aim test off";
            }
            if (verb != "on") { return "aim test " + (testAiming ? "on" : "off") + " crouched=" + testCrouched + " cover=" + testCover + " speed=" + (testSpeed.HasValue ? testSpeed.Value.ToString("0.0") : "-"); }
            testAiming = true; testCrouched = false; testCover = false; testSpeed = null;
            for (int index = 1; index < args.Length; index++)
            {
                if (args[index] == "crouched") { testCrouched = true; }
                else if (args[index] == "cover") { testCover = true; }
                else if (args[index] == "speed" && index + 1 < args.Length)
                {
                    double speed;
                    if (!double.TryParse(args[index + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out speed)) { return "error: bad speed " + args[index + 1]; }
                    testSpeed = speed;
                    index++;
                }
                else { return "error: aim on [crouched] [cover] [speed <m/s>]"; }
            }
            RuntimeLog.Info("aim_test on crouched=" + testCrouched + " cover=" + testCover + " speed=" + (testSpeed.HasValue ? testSpeed.Value.ToString("0.0") : "-"));
            return "aim test on crouched=" + testCrouched + " cover=" + testCover + " speed=" + (testSpeed.HasValue ? testSpeed.Value.ToString("0.0") : "-");
        }

        private string ReticleCommand(string[] args)
        {
            string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            if (verb == "debug")
            {
                reticleDebug = args.Length > 1 && args[1] == "on";
                if (reticleDebug) { reticleTruth.Reset(); }
                RuntimeLog.Info("reticle_debug " + (reticleDebug ? "on" : "off"));
                return "reticle debug " + (reticleDebug ? "on" : "off");
            }
            if (verb == "reset") { reticleTruth.Reset(); return "reticle truth counters reset"; }
            string text = "class=" + (activeReticle != null ? activeReticle.WeaponClass : "-") + " style=" + (activeReticle != null ? activeReticle.Style : "-") + " drawn=" + drawCrosshair +
                " samples=" + reticleTruth.Samples + " steady=" + reticleTruth.SteadySamples + " violations=" + reticleTruth.Violations + " under_reports=" + reticleTruth.UnderReports +
                " worst_error_pct=" + reticleTruth.WorstErrorPercent.ToString("0.00");
            if (verb == "check")
            {
                RuntimeLog.Info("reticle_check " + text);
                if (reticleTruth.SteadySamples == 0) { return "error: no steady reticle frame was sampled (debug on, aim on, wait) " + text; }
                if (reticleTruth.Violations > 0 || reticleTruth.UnderReports > 0) { return "error: the drawn reticle disagrees with the cone " + text; }
                return "reticle ok " + text;
            }
            return "reticle " + text;
        }

        // `swap left|right|toggle|status|probe` (T-042): pick a shoulder without the aim button and prove the camera table follows.
        private string SwapCommand(string[] args)
        {
            string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            if (shoulderSwap == null || shoulderSettings == null) { return "error: shoulder swap is unavailable (aim camera table not validated)"; }
            if (verb == "left") { shoulderSwap.Force(true); return "swap left requested"; }
            if (verb == "right") { shoulderSwap.Force(false); return "swap right requested"; }
            if (verb == "toggle") { shoulderSwap.Force(!shoulderSwap.Left); return "swap " + (shoulderSwap.Left ? "left" : "right") + " requested"; }
            string detail;
            bool live = shoulderSettings.LiveMatchesApplied(out detail);
            bool settled = Math.Abs(shoulderSwap.Current - shoulderSwap.Target) < 0.001 && Math.Abs(shoulderSettings.AppliedFactor - shoulderSwap.Target) < 0.001;
            string text = "side=" + (shoulderSwap.Left ? "left" : "right") + " target=" + shoulderSwap.Target.ToString("0.00") + " applied=" + shoulderSettings.AppliedFactor.ToString("0.000") +
                " settled=" + settled + " live_ok=" + live + " " + detail;
            RuntimeLog.Info("swap_probe " + text);
            if (verb == "probe" && (!live || !settled)) { return "error: " + text; }
            return "swap " + text;
        }

        private string RangeCommand(string[] args)
        {
            string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            System.Globalization.CultureInfo invariant = System.Globalization.CultureInfo.InvariantCulture;
            try
            {
                if (verb == "start")
                {
                    Ped ped = Player.Character;
                    if (ped == null) { return "error: no player"; }
                    if (args.Length >= 3 && args[1] == "ahead")
                    {
                        double distance = double.Parse(args[2], invariant);
                        double height = args.Length > 3 ? double.Parse(args[3], invariant) : 0.3;
                        double heading = ped.Heading * Math.PI / 180.0;
                        Vector3 position = ped.Position;
                        // GTA headings: 0 = north (+Y), increasing counter-clockwise.
                        rangeAim = new Vec3(position.X - Math.Sin(heading) * distance, position.Y + Math.Cos(heading) * distance, position.Z + height);
                    }
                    else if (args.Length >= 4) { rangeAim = new Vec3(double.Parse(args[1], invariant), double.Parse(args[2], invariant), double.Parse(args[3], invariant)); }
                    else { return "error: range start ahead <m> [height] | start <x> <y> <z>"; }
                    rangeRecorder.Shots.Clear();
                    rangeRecorder.Reset();
                    // Shots at the range are aimed shots: the model and reticle treat the player as aiming while it runs.
                    testAiming = true;
                    rangeStartedMilliseconds = Now;
                    RuntimeLog.Info("range_started aim=" + rangeAim.Value + " weapon=" + activeWeaponId + " profile=" + (activeProfile != null ? activeProfile.ProfileName : "vanilla"));
                    return "range aim " + rangeAim.Value;
                }
                if (verb == "fire")
                {
                    if (!rangeAim.HasValue) { return "error: range start first"; }
                    int milliseconds = args.Length > 1 ? int.Parse(args[1], invariant) : 1000;
                    int mode = args.Length > 2 ? int.Parse(args[2], invariant) : 4;
                    if (mode < 0 || mode > 4 || milliseconds <= 0 || milliseconds > 30000) { return "error: range fire <1-30000 ms> [mode 0-4]"; }
                    // TASK_SHOOT_AT_COORD(ped, x, y, z, duration ms, mode): 0 aim only, 1 single, 2 single keeping aim, 3 burst, 4 continuous.
                    GTA.Native.Function.Call("TASK_SHOOT_AT_COORD", Player.Character, (float)rangeAim.Value.X, (float)rangeAim.Value.Y, (float)rangeAim.Value.Z, milliseconds, mode);
                    RuntimeLog.Info("range_fire ms=" + milliseconds + " mode=" + mode);
                    return "firing " + milliseconds + " ms in mode " + mode;
                }
                if (verb == "stop")
                {
                    int count = rangeRecorder.Count;
                    foreach (string line in rangeRecorder.Summary()) { RuntimeLog.Info("range_summary " + line); }
                    RuntimeLog.Info("range_stopped shots=" + count + " seconds=" + ((Now - rangeStartedMilliseconds) / 1000.0).ToString("0.0"));
                    rangeAim = null;
                    testAiming = false;
                    return "range stopped: " + count + " shots";
                }
                if (verb == "status") { return rangeAim.HasValue ? "range active: " + rangeRecorder.Count + " shots, aim " + rangeAim.Value : "range off"; }
                return "error: unknown range verb " + verb;
            }
            catch (Exception error)
            {
                RuntimeLog.Error("range_command_failed verb=" + verb + " error=" + error.Message);
                return "error: " + error.Message;
            }
        }

        // Each of the player's new bullets: deviation from the line to the aim point, against the cone the model had written.
        private void RecordRange(List<BulletLog.Trace> traces, int weaponId, double now)
        {
            if (!rangeAim.HasValue) { return; }
            double chainReset = activeProfile != null ? activeProfile.Spread.ChainResetMilliseconds : 450;
            foreach (BulletLog.Trace trace in traces)
            {
                Vec3 intended = rangeAim.Value - trace.Start;
                Vec3 actual = trace.End - trace.Start;
                if (intended.Length < 0.5 || actual.Length < 0.5) { continue; }
                double cosine = Math.Max(-1.0, Math.Min(1.0, Vec3.Dot(intended, actual) / (intended.Length * actual.Length)));
                double deviation = Math.Acos(cosine) * 180.0 / Math.PI;
                RangeShot shot = rangeRecorder.Add(weaponId, now, deviation, writtenConeDegrees, chainReset);
                RuntimeLog.Info("range_shot weapon=" + weaponId + " n=" + shot.Index + " gap_ms=" + shot.GapMilliseconds.ToString("0") + " dev=" + deviation.ToString("0.000") +
                    " cone=" + shot.ConeDegrees.ToString("0.000") + " range_m=" + actual.Length.ToString("0.0") + " state=" + state.Describe());
            }
        }

        private void AuditBullets(GunplayConfig config, int gameCamera, int weaponId, double now)
        {
            if (bullets == null || playerPed == 0) { return; }
            List<BulletLog.Trace> traces = bullets.ReadNew(playerPed);
            if (traces.Count == 0)
            {
                if (bullets.LastTotal > 0 && bullets.LastOwnedCount == 0 && !loggedOwnerMismatch && now - lastShotMilliseconds < 100)
                {
                    loggedOwnerMismatch = true;
                    RuntimeLog.Error("shot_audit_owner_mismatch player_ped=0x" + playerPed.ToString("X8") + " list_total=" + bullets.LastTotal +
                        " first_owner=0x" + bullets.LastForeignOwner.ToString("X8"));
                }
                return;
            }
            if (rangeAim.HasValue) { RecordRange(traces, weaponId, now); }
            if (!gameCameraValid) { return; }
            Vec3 cameraPosition = Natives.CamPosition(gameCamera);
            Vec3 rotation = Natives.CamRotation(gameCamera);
            Vec3 forward = Vec3.FromPitchHeadingDegrees(rotation.X, rotation.Z);
            Camera currentCamera = (!loggedCameraCrossCheck || useShdnDirection) ? Game.CurrentCamera : null;
            bool sameCamera = currentCamera != null && currentCamera.GetHashCode() == gameCamera;
            if (!loggedCameraCrossCheck && !sameCamera && !loggedCameraCrossCheckDeferred)
            {
                loggedCameraCrossCheckDeferred = true;
                RuntimeLog.Info("shot_audit_camera_check_deferred reason=different_camera_handles");
            }
            if (!loggedCameraCrossCheck && sameCamera)
            {
                // Compare the same camera. A scripted side-on review camera is not evidence of a rotation convention mismatch.
                loggedCameraCrossCheck = true;
                Vector3 shdn = currentCamera.Direction;
                Vec3 other = new Vec3(shdn.X, shdn.Y, shdn.Z).Normalized();
                double agreement = Math.Acos(Math.Max(-1, Math.Min(1, Vec3.Dot(forward, other)))) * 180 / Math.PI;
                Vec3 firstShot = (traces[0].End - traces[0].Start).Normalized();
                RuntimeLog.Info("shot_audit_camera_check rot=" + rotation + " forward=" + forward + " shdn_forward=" + other +
                    " angle_between=" + agreement.ToString("0.00") + " first_bullet_dir=" + firstShot);
                if (agreement > 5) { useShdnDirection = true; RuntimeLog.Error("shot_audit_convention_mismatch using ScriptHookDotNet camera direction"); }
            }
            if (useShdnDirection && sameCamera)
            {
                Vector3 shdn = currentCamera.Direction;
                forward = new Vec3(shdn.X, shdn.Y, shdn.Z);
            }
            ShotAuditStats stats;
            if (!audits.TryGetValue(weaponId, out stats)) { stats = new ShotAuditStats(); audits[weaponId] = stats; }
            WeaponProfile profile = ResolveProfile(config, weaponId);
            // Bullets fired this frame used the accuracy written (and the crosshair shown) last frame.
            double intended = writtenConeDegrees;
            double shown = profile != null ? displayConeDegrees : currentConeDegrees;
            foreach (BulletLog.Trace trace in traces)
            {
                if (DebugOverlay) { debugHit.LastTraceDistanceMeters = (trace.End - trace.Start).Length; }
                double deviation = ShotGeometry.DeviationDegrees(cameraPosition, forward, trace.Start, trace.End);
                if (double.IsNaN(deviation)) { continue; }
                stats.Add(deviation, shown);
                bool gainChanged = false;
                if (profile != null && profile.CalibrationSource && SpreadControlEnabled && weaponInfo != null && weaponInfo.CanWrite(profile.WeaponId))
                {
                    gainChanged = calibrator.AddSample(config.SpreadCalibration, deviation, intended);
                }
                if (stats.TotalBullets <= DetailedAuditBullets || stats.TotalBullets % 10 == 0 || gainChanged)
                {
                    RuntimeLog.Info("shot_audit weapon=" + weaponId + " dev=" + deviation.ToString("0.000") + " cone=" + intended.ToString("0.000") + " shown=" + shown.ToString("0.000") +
                        " accuracy=" + writtenAccuracy.ToString("0.0000") + " gain=" + calibrator.Gain.ToString("0.000") + " state=" + state.Describe() +
                        " range_m=" + (trace.End - trace.Start).Length.ToString("0.0") + (gainChanged ? " gain_updated ratio=" + calibrator.LastEstimatedRatio.ToString("0.000") : ""));
                }
                if (stats.TotalBullets % 20 == 0) { RuntimeLog.Info("shot_audit_summary weapon=" + weaponId + " " + stats.Summary()); }
            }
        }

        private void UpdateSpread(GunplayConfig config, double now, double deltaSeconds)
        {
            if (activeProfile != null)
            {
                currentConeDegrees = spread.Step(activeProfile.Spread, config.Movement, state, now, deltaSeconds);
                if (SpreadControlEnabled && weaponInfo != null && weaponInfo.CanWrite(activeProfile.WeaponId))
                {
                    // Hold the vanilla aim-settle timer at zero so the configured cone is what the game applies.
                    if (playerMemory != null) { playerMemory.ClearAimSettle(playerPed); }
                    writtenAccuracy = calibrator.ToAccuracy(config.SpreadCalibration, currentConeDegrees);
                    weaponInfo.WriteAccuracy(activeProfile.WeaponId, (float)writtenAccuracy);
                    writtenConeDegrees = currentConeDegrees;
                    displayConeDegrees = currentConeDegrees + PelletAllowance(activeProfile);
                }
                else
                {
                    // Spread control unavailable: show what the game really uses (its own accuracy).
                    float accuracy;
                    displayConeDegrees = weaponInfo != null && weaponInfo.TryReadAccuracy(activeProfile.WeaponId, out accuracy) ?
                        calibrator.FromAccuracy(config.SpreadCalibration, accuracy) : currentConeDegrees;
                    writtenConeDegrees = displayConeDegrees;
                }
            }
            else
            {
                float accuracy;
                // Vanilla weapons keep the game's own aim settle, so show the cone it will actually apply.
                double settle = playerMemory != null ? playerMemory.AimSettleFactor(playerPed) : 1.0;
                currentConeDegrees = weaponInfo != null && weaponInfo.TryReadAccuracy(activeWeaponId, out accuracy) ?
                    calibrator.FromAccuracy(config.SpreadCalibration, accuracy) * settle : 0;
                displayConeDegrees = currentConeDegrees;
                writtenConeDegrees = currentConeDegrees;
                writtenAccuracy = 0;
            }
        }

        // Multi-pellet weapons: the pellet pattern on top of the controlled cone, measured once enough shots exist.
        private double PelletAllowance(WeaponProfile profile)
        {
            if (profile.Spread.PelletPatternDegrees <= 0) { return 0; }
            ShotAuditStats stats;
            if (audits.TryGetValue(profile.WeaponId, out stats) && stats.WindowCount >= 16 && store.Active.SpreadCalibration.AutoCalibrate)
            {
                return Math.Max(0, stats.MaximumMeasuredDegrees - writtenConeDegrees);
            }
            return profile.Spread.PelletPatternDegrees;
        }

        private void DisableCamera(Exception error)
        {
            RuntimeLog.Error("feature_disabled camera=" + (aimCamera != null ? aimCamera.Name : "?") + " error=" + error);
            if (aimCamera == onFootCamera) { onFootCamera = null; } else { vehicleCamera = null; }
            aimCamera = null;
        }

        private void DisableSpread(Exception error)
        {
            RuntimeLog.Error("feature_disabled spread_control error=" + error);
            try { if (weaponInfo != null) { weaponInfo.RestoreAll(); } }
            catch (Exception restoreError) { RuntimeLog.Error("restore_weaponinfo_failed error=" + restoreError.Message); }
            weaponInfo = null;
        }

        private void DisableHud(Exception error)
        {
            RuntimeLog.Error("feature_disabled hud_reticle error=" + error);
            drawCrosshair = false;
            try { if (hud != null) { hud.RestoreAll(); } }
            catch (Exception restoreError) { RuntimeLog.Error("restore_hud_failed error=" + restoreError.Message); }
            hud = null;
        }

        private bool KickReady(GunplayConfig config)
        {
            return aimCamera != null && aimCamera.Validated && CameraKickEnabled && config.RecoilGlobal.EnableCameraKick &&
                (!realRecoilPresent || config.RecoilGlobal.AllowWithRealRecoil);
        }

        // T-026: one-time wall-clock cost of a script native call from this script (two trivial natives, 50 each).
        private static void LogNativeCost(Player player)
        {
            const int Calls = 50;
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < Calls; i++) { Natives.IsPlayerPlaying(player); }
            long playing = Stopwatch.GetTimestamp() - start;
            start = Stopwatch.GetTimestamp();
            for (int i = 0; i < Calls; i++) { Natives.GameCamHandle(); }
            long camera = Stopwatch.GetTimestamp() - start;
            RuntimeLog.Info("native_cost calls=" + Calls + " is_player_playing_us=" + (playing * 1000000.0 / Stopwatch.Frequency / Calls).ToString("0.0") +
                " get_game_cam_us=" + (camera * 1000000.0 / Stopwatch.Frequency / Calls).ToString("0.0") +
                " thread=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
        }

        // The game camera handle only changes on camera-mode switches (cutscenes, phone, death). GET_GAME_CAM and
        // DOES_CAM_EXIST are re-asked at the configured cadence; in between, the handle's camera-pool slot and
        // generation byte (read from memory, no native) prove the cached camera still exists.
        private int CurrentGameCamera(GunplayConfig config, double now)
        {
            double refresh = config.Performance != null ? config.Performance.GameCameraRefreshMilliseconds : 0;
            AimCamera validator = onFootCamera != null ? onFootCamera : vehicleCamera;
            if (cachedGameCamera != 0 && validator != null && now - lastGameCameraReadMilliseconds < refresh)
            {
                gameCameraValid = validator.CameraFromHandle(cachedGameCamera) != 0;
                if (gameCameraValid) { return cachedGameCamera; }
            }
            cachedGameCamera = Natives.GameCamHandle();
            lastGameCameraReadMilliseconds = now;
            gameCameraValid = Natives.CamExists(cachedGameCamera);
            return cachedGameCamera;
        }

        private void UpdateRecoil(GunplayConfig config, double now, double deltaSeconds, uint aimCam, int gameCamera)
        {
            if (aimCamera != null && aimCam != 0 && !aimCamera.Validated && !aimCamera.Rejected &&
                now - lastShotMilliseconds > 400 && gameCameraValid)
            {
                aimCamera.Sample(aimCam, Natives.CamRotation(gameCamera), config.RecoilGlobal.CameraValidationToleranceDegrees,
                    config.RecoilGlobal.CameraValidationSamples);
            }
            if (activeProfile == null) { return; }
            bool counterSteering = Math.Abs(controller.RightY) >= config.RecoilGlobal.RecoveryCancelStickThreshold;
            RecoilStep step = recoil.Step(activeProfile.Recoil, now, deltaSeconds, counterSteering);
            if (step.IsZero || aimCam == 0 || !KickReady(config) || LibertyFramework.DevTools.DevToolsMenu.IsOpen) { return; }
            aimCamera.AddDegrees(aimCam, step.PitchDegrees, step.HeadingDegrees);
        }

        // T-017: a small transient kick follows the recoil step, so the latter retains its
        // predictable pattern. The FOV is saved once on entry and restored on exit/error.
        private void UpdateFeel(GunplayConfig config, int shots, double deltaSeconds, uint aimCam, int gameCamera)
        {
            if (feelDisabled) { return; }
            FeelSettings settings = config.Feel;
            bool active = settings.Enabled && activeProfile != null && aiming && !state.InVehicle &&
                !LibertyFramework.DevTools.DevToolsMenu.IsOpen && aimCam != 0 && aimCamera != null && aimCamera.Validated;
            if (!active) { RestoreFeel(); return; }
            if (shots > 0)
            {
                double pitch = (feelRandom.NextDouble() * 2 - 1) * settings.ShakePitchDegrees * shots;
                double heading = (feelRandom.NextDouble() * 2 - 1) * settings.ShakeHeadingDegrees * shots;
                aimCamera.AddDegrees(aimCam, pitch, heading);
            }
            GTA.Camera camera = Game.CurrentCamera;
            if (camera == null) { return; }
            if (!fovApplied || feelCameraHandle != gameCamera)
            {
                RestoreFeel();
                feelCamera = camera;
                feelCameraHandle = gameCamera;
                originalFov = camera.FOV;
                fovApplied = true;
                SaveFeelState();
            }
            if (originalFov <= settings.AimFovReductionDegrees) { return; }
            double desired = originalFov - settings.AimFovReductionDegrees;
            double fraction = Math.Min(1, Math.Max(0, deltaSeconds * settings.FovSmoothingPerSecond));
            camera.FOV = (float)(camera.FOV + (desired - camera.FOV) * fraction);
        }

        private void RestoreFeel()
        {
            if (!fovApplied) { return; }
            fovApplied = false;
            GTA.Camera camera = feelCamera;
            feelCamera = null;
            feelCameraHandle = 0;
            if (camera != null && camera.Exists()) { camera.FOV = originalFov; }
            if (File.Exists(fovStatePath)) { File.Delete(fovStatePath); }
        }

        private void SaveFeelState()
        {
            using (Process process = Process.GetCurrentProcess())
            {
                FeelFovRestoreState state = new FeelFovRestoreState();
                state.ProcessId = process.Id;
                state.ProcessStartUtcTicks = process.StartTime.ToUniversalTime().Ticks;
                state.CameraHandle = feelCameraHandle;
                state.OriginalFov = originalFov;
                JsonStore.Save(fovStatePath, state);
            }
        }

        private void RecoverFeel()
        {
            if (!File.Exists(fovStatePath)) { return; }
            FeelFovRestoreState state = JsonStore.Load<FeelFovRestoreState>(fovStatePath);
            using (Process process = Process.GetCurrentProcess())
            {
                if (state.ProcessId == process.Id && state.ProcessStartUtcTicks == process.StartTime.ToUniversalTime().Ticks &&
                    state.CameraHandle == Natives.GameCamHandle() && Game.CurrentCamera != null)
                {
                    Game.CurrentCamera.FOV = state.OriginalFov;
                    RuntimeLog.Info("feel_fov_recovered fov=" + state.OriginalFov);
                }
            }
            File.Delete(fovStatePath);
        }

        private void UpdateReticle(GunplayConfig config, Ped ped)
        {
            // After a drawing failure the LF crosshair is gone, so the vanilla reticle comes back.
            bool replace = CrosshairEnabled && !drawFailed && config.Crosshair.ReplaceVanillaReticle && hud != null;
            if (replace)
            {
                hud.Hide(HudReticle.Crosshair);
                hud.Hide(HudReticle.Dot);
            }
            else if (hud != null)
            {
                hud.Restore(HudReticle.Crosshair);
                hud.Restore(HudReticle.Dot);
            }
            bool gun = IsCrosshairWeapon(ped);
            bool showForWeapon = activeProfile != null || config.Crosshair.ShowForVanillaWeapons;
            activeReticle = ResolveReticle(config, ped);
            ReticleSettings rules = config.Reticles != null && config.Reticles.Enabled ? config.Reticles : null;
            // Legacy (no reticles section): drawn only while aiming. With it: not aiming is hidden or reduced, a vehicle can hide it.
            bool visibleByAim = aiming || (rules != null && rules.NotAiming == "reduced");
            bool hiddenInVehicle = rules != null && rules.HideInVehicle && state.InVehicle;
            reticleOpacity = aiming || rules == null ? 1.0 : rules.NotAimingOpacity;
            drawCrosshair = replace && gun && showForWeapon && visibleByAim && !hiddenInVehicle && !activeReticle.IsNone && !LibertyFramework.DevTools.DevToolsMenu.IsOpen &&
                !Natives.IsPauseMenuActive() && !Natives.IsScreenFadedOut() && Natives.IsPlayerControlOn(Player);
            if (drawCrosshair && (reticleDebug || (rules != null && rules.DebugLog))) { SampleReticle(); }
        }

        // The reticle of the held weapon: catalog class (else the inventory slot), style from gunplay.json, cached per weapon and config.
        private ResolvedReticle ResolveReticle(GunplayConfig config, Ped ped)
        {
            // The weapon id the tick already read (a ScriptHookDotNet inventory call here would cost every frame).
            int weaponId = activeWeaponId;
            if (activeReticle == null || weaponId != reticleWeaponId || !ReferenceEquals(config, reticleConfig) || !ReferenceEquals(weaponCatalog, reticleCatalog))
            {
                GTA.value.Weapon current = ped.Weapons.Current;
                string weaponClass = ReticleResolver.ClassOf(weaponCatalog, weaponId, current == null ? "pistol" : SlotClass(current.Slot));
                activeReticle = ReticleResolver.Resolve(config, weaponClass, weaponId);
                reticleWeaponId = weaponId;
                reticleConfig = config;
                reticleCatalog = weaponCatalog;
                crosshair.Reset();
                reticleTruth.Reset();
                RuntimeLog.Info("reticle_resolved weapon=" + weaponId + " class=" + weaponClass + " style=" + activeReticle.Style + " min_px=" + activeReticle.MinimumGapPixels.ToString("0.#") +
                    " max_px=" + activeReticle.MaximumGapPixels.ToString("0.#") + " length_px=" + activeReticle.LineLengthPixels.ToString("0.#"));
            }
            return activeReticle;
        }

        // Class of a weapon that is not in the catalog: from its inventory slot (the sniper slot has no reticle of its own: the game's scope).
        private static string SlotClass(WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.Shotgun: return "shotgun";
                case WeaponSlot.SMG: return "smg";
                case WeaponSlot.Rifle: return "rifle";
                case WeaponSlot.Sniper: return "sniper";
                case WeaponSlot.Heavy: return "heavy";
                case WeaponSlot.Thrown: return "thrown";
                default: return "pistol";
            }
        }

        // Frames the drawn opening against the cone (T-043 truthfulness), from values the draw pass left behind.
        private void SampleReticle()
        {
            double now = Now;
            double target = crosshair.LastTargetPixels;
            double drawn = crosshair.DisplayedGapPixels;
            if (target <= 0 || drawn < 0) { return; }
            bool steady = reticleTruth.Add(now, target, drawn);
            if (now - lastReticleLogMilliseconds >= 250)
            {
                lastReticleLogMilliseconds = now;
                RuntimeLog.Info("reticle_frame weapon=" + reticleWeaponId + " class=" + activeReticle.WeaponClass + " style=" + activeReticle.Style + " cone=" + displayConeDegrees.ToString("0.000") +
                    " target_px=" + target.ToString("0.00") + " drawn_px=" + drawn.ToString("0.00") + " steady=" + steady + " state=" + state.Describe());
            }
        }

        private static bool IsCrosshairWeapon(Ped ped)
        {
            GTA.value.Weapon current = ped.Weapons.Current;
            if (current == null) { return false; }
            switch (current.Slot)
            {
                case WeaponSlot.Handgun:
                case WeaponSlot.Shotgun:
                case WeaponSlot.SMG:
                case WeaponSlot.Rifle:
                case WeaponSlot.Heavy:
                case WeaponSlot.Thrown:
                    return true;
                default:
                    // Sniper rifles use the scope overlay (HUD_WEAPON_SCOPE), which is left untouched.
                    return false;
            }
        }

        // ScriptHookDotNet raises PerFrameDrawing from its Direct3D hook, outside the script tick, so a
        // drawing error only stops drawing here; natives and engine restores stay on the tick.
        private void OnDraw(object sender, GraphicsEventArgs args)
        {
            if (disabled || drawFailed) { return; }
            try
            {
                GunplayConfig config = store.Active;
                if (config == null) { return; }
                if (drawCrosshair)
                {
                    long drawStart = Stopwatch.GetTimestamp();
                    ResolvedReticle style = activeReticle;
                    if (style != null) { crosshair.Draw(args.Graphics, style, config.Crosshair.FovAxis, reticleOpacity, displayConeDegrees, lastFov, pixelsPerTangent, screenResolution, args.Graphics.FrameTime); }
                    CostMeter.Add("draw.crosshair", drawStart);
                }
                if (DebugOverlay) { DrawOverlay(args.Graphics); }
            }
            catch (Exception error)
            {
                drawFailed = true;
                drawCrosshair = false;
                RuntimeLog.Error("feature_disabled drawing (crosshair and overlay; vanilla reticle restored next tick) error=" + error);
            }
        }

        private void DrawOverlay(GTA.Graphics graphics)
        {
            graphics.Scaling = FontScaling.Pixel;
            List<string> lines = StatusLines();
            float height = 22 * lines.Count + 12;
            graphics.DrawRectangle(new RectangleF(20, 20, 560, height), Color.FromArgb(150, 0, 0, 0));
            for (int index = 0; index < lines.Count; index++)
            {
                graphics.DrawText(lines[index], new RectangleF(30, 26 + index * 22, 540, 22), TextAlignment.Left, Color.White);
            }
        }

        internal List<string> StatusLines()
        {
            List<string> lines = new List<string>();
            GunplayConfig config = store.Active;
            lines.Add("WEAPON " + activeWeaponId + " " + TestWeaponActions.LabelFor(config, activeWeaponId) +
                (activeProfile != null ? "  profile=" + activeProfile.ProfileName : "  (vanilla)"));
            lines.Add("Preset " + (store.ActivePresetName ?? "-") + "  finish=" + WeaponFinishCatalog.Describe(activeProfile));
            lines.Add("Aim " + (FreeAimEnabled ? "FREE AIM" : "vanilla") + "  aiming=" + aiming + " aimcam=" + aimCameraActive + "  " + state.Describe());
            lines.Add("Spread cone=" + currentConeDegrees.ToString("0.00") + " shown=" + displayConeDegrees.ToString("0.00") + " deg  accuracy=" +
                writtenAccuracy.ToString("0.000") + "  gain=" + calibrator.Gain.ToString("0.00"));
            ShotAuditStats stats;
            if (audits.TryGetValue(activeWeaponId, out stats)) { lines.Add("Audit " + stats.Summary()); }
            else { lines.Add("Audit no bullets measured yet for this weapon"); }
            lines.Add("Recoil chain=" + recoil.ChainShots + " climb=" + recoil.AccumulatedPitchDegrees.ToString("0.00") + " recoverable=" +
                recoil.RecoverablePitchDegrees.ToString("0.00") + " last_kick=" + recoil.LastKickPitchDegrees.ToString("0.00"));
            lines.Add("Hit " + (debugHitDisabled ? "unavailable" : debugHit.LastHit) + "  fire=" +
                (fireIntervalMilliseconds > 0 ? fireIntervalMilliseconds.ToString("0") + "ms / " + (60000.0 / fireIntervalMilliseconds).ToString("0") + " RPM" : "waiting"));
            lines.Add("Camera " + (aimCamera == null ? "none" : aimCamera.Name + " " + (aimCamera.Validated ? "validated" : aimCamera.Rejected ? "REJECTED" : "validating")) +
                "  kick=" + (config != null && KickReady(config) ? "on" : "off") + (realRecoilPresent ? "  REAL RECOIL DETECTED" : ""));
            lines.Add("Engine prefs=" + (prefs != null) + " lockon=" + (playerMemory != null) + " hud=" + (hud != null) + " weaponinfo=" +
                (weaponInfo != null && weaponInfo.Validated) + " bullets=" + (bullets != null) + (drawFailed ? " DRAWING OFF" : "") +
                (disabled ? " DISABLED " + disabledReason : ""));
            if (freeAim != null && prefs != null)
            {
                bool? lockOn = playerMemory != null ? playerMemory.LockOnDisabled(playerIndex) : null;
                lines.Add("FreeAim active=" + freeAim.Active + " auto_aim_pref=" + prefs.AutoAim + " (was " + freeAim.AutoAimPrior + ") lockon_disabled=" +
                    (lockOn.HasValue ? lockOn.Value.ToString() : "?") + " reticle_hidden=" + (hud != null && hud.IsHidden(HudReticle.Crosshair)));
            }
            if (store.LastError != null) { lines.Add("Config error: " + store.LastError); }
            return lines;
        }

        internal void ResetCalibration()
        {
            calibrator.Reset();
            foreach (ShotAuditStats stats in audits.Values) { stats.Reset(); }
            RuntimeLog.Info("calibration_reset");
        }

        internal void OnProfilesChanged()
        {
            recoil.Reset();
            spread.Reset();
        }

        private void DisableAfterError(Exception error)
        {
            disabled = true;
            disabledReason = error.GetType().Name;
            RuntimeLog.Error("gunplay_disabled error=" + error);
            try { RestoreFeel(); }
            catch (Exception restoreError) { RuntimeLog.Error("restore_feel_failed error=" + restoreError); }
            RestoreEngine(SafePlayer());
        }

        // Memory restores run first; FreeAimMode calls the lock-on native last and only when 'player' is set,
        // so a failing native (or none at process exit) can never leave the Auto-Aim pref or reticle altered.
        private void RestoreEngine(Player player)
        {
            // At unload only memory restores are legal; FOV is restored on the preceding tick
            // when aim ends. Process exit discards the engine camera instance.
            try { if (weaponInfo != null) { weaponInfo.RestoreAll(); } }
            catch (Exception error) { RuntimeLog.Error("restore_weaponinfo_failed error=" + error.Message); }
            try { if (hud != null) { hud.RestoreAll(); } }
            catch (Exception error) { RuntimeLog.Error("restore_hud_failed error=" + error.Message); }
            try { if (shoulderSwap != null) { shoulderSwap.Restore(); } }
            catch (Exception error) { RuntimeLog.Error("restore_shoulder_failed error=" + error.Message); }
            try { if (freeAim != null) { freeAim.Disable(player); } }
            catch (Exception error) { RuntimeLog.Error("restore_freeaim_failed error=" + error.Message); }
        }

        private Player SafePlayer()
        {
            try { return Player; }
            catch (Exception error)
            {
                RuntimeLog.Error("restore_player_unavailable lock-on restore skipped error=" + error.Message);
                return null;
            }
        }

        private void OnDomainUnload(object sender, EventArgs args)
        {
            RuntimeLog.Info("gunplay_unloading restoring engine state");
            RestoreEngine(SafePlayer());
        }

        // At process exit the game is shutting down on another thread: restore memory only, call no natives.
        private void OnProcessExit(object sender, EventArgs args)
        {
            RuntimeLog.Info("gunplay_process_exit restoring engine memory (no natives)");
            RestoreEngine(null);
        }
    }
}
