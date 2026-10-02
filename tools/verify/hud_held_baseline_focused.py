"""Generate a small exact-method HUD baseline harness from the lane root.

Compile/run instructions and evidence limits are in the held-baseline offline receipt.
SDK structs are real; game calls are stubs. This does not build production binaries.
"""
from pathlib import Path
import json
import re
root = Path.cwd()
out = root / 'results-local/offline/sol-hud-held-baseline'
out.mkdir(parents=True, exist_ok=True)
source = (root / 'src/LibertyFramework/Hud/HudModule.cs').read_text()
def method(text, signature):
    start = text.index(signature)
    brace = text.index('{', start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]
methods = '\n'.join(method(source, s) for s in ['private bool BaselineGameplaySafe', 'private void UpdateBaselineTask', 'private string Baseline(', 'private void ReleaseBaselineTask', 'private void RetireExpiredBaselineTask'])
checks = method((root / 'tools/verify/Stage1HudChecks.cs').read_text(), 'private static void BaselineReadback')
prefix = r'''
using System; using System.Collections.Generic; using System.Globalization;
using Liberty.Sdk; using LibertyFramework.Hud.Logic; using LibertyFramework.Verify;
static class RuntimeLog { internal static void Info(string value) {} }
class World { internal bool HasPlayer=true; internal PlayerState Player=new PlayerState { Ped=new PedRef(10), Weapon=7, Health=100, IsPlaying=true, HasControl=true, Position=new Vec3(10,20,30), Heading=0 }; internal WorldInfo Info; }
class Weapons { internal int Held=7, Clip=17, Total=150, Reads; internal bool Owned=true; internal int Current(PedRef p) { Reads++; return Held; } internal bool Has(PedRef p,int w) { Reads++; return Owned; } internal int GetAmmoInClip(PedRef p,int w) { Reads++; return Clip; } internal int GetAmmo(PedRef p,int w) { Reads++; return Total; } }
class Tasks { internal int Idle,Aim,Clears,Duration; internal PedRef Cleared; internal Vec3 Target; internal void StandStill(PedRef p,int ms) { Idle++; Duration=ms; } internal void AimAt(PedRef p,Vec3 target,int ms) { Aim++; Target=target; Duration=ms; } internal void Clear(PedRef p) { Clears++; Cleared=p; } }
class Peds { internal bool Present=true; internal bool Exists(PedRef p) { return Present; } }
class Owner { internal string Id="fixture"; }
class Input { internal Owner CapturedBy; internal float LeftTrigger; internal bool KeyDown(VirtualKey k) { return false; } }
class Cameras { internal Vec3 GameCameraPosition,GameCameraRotation; internal float GameCameraFov=60; }
class Api { internal World World=new World(); internal Weapons Weapons=new Weapons(); internal Tasks Tasks=new Tasks(); internal Peds Peds=new Peds(); internal Input Input=new Input(); internal Cameras Cameras=new Cameras(); }
class Ui { internal bool AnyMenuOpen,OtherOwner; internal bool HudHiddenByOtherOwner(object p) { return OtherOwner; } }
class EngineStub { internal Ui Ui=new Ui(); }
class ClockStub { internal long ElapsedMilliseconds; }
class ProbeModule {
internal Api Liberty=new Api(); internal EngineStub Engine=new EngineStub(); internal HudConfig config=HudConfig.Defaults();
internal bool disabled,probeMode=true,layoutTest; internal HudNativeDisplayProbe nativeDisplay=new HudNativeDisplayProbe();
internal int? cashTestOriginal; internal PedRef baselineTaskPed=PedRef.None; internal string baselineTask="none";
internal long baselineTaskExpiresMilliseconds; internal ClockStub clock=new ClockStub();
HashSet<string> planHidden=new HashSet<string>(),probeHidden=new HashSet<string>(); const int RightMouseButton=2;
internal string Run(string args) { return Baseline(args.Split(' ')); }
internal void Tick() { UpdateBaselineTask(); } internal void Release() { ReleaseBaselineTask(); }
'''
tests = r'''
class Focused {
static void True(Checker c,string name,bool value) { c.True(name,value,""); }
static ProbeModule Ready() { return new ProbeModule(); }
static int Main() {
var c=new Checker(); BaselineReadback(c);
var p=Ready(); p.Liberty.Weapons.Held=0; p.Liberty.World.Player.Weapon=0;
string s=p.Run("baseline state 7"); True(c,"inventory remains separate from selected readback",s.Contains("held=0 snapshot=0 owned=True clip=17 total=150 numeric_ready=False") && s.Contains("visual=unproven"));
True(c,"unarmed check refuses fixture",p.Run("baseline check 7").StartsWith("error:"));
True(c,"unarmed task refused without mutation",p.Run("baseline idle 7 100").StartsWith("error:") && p.Liberty.Tasks.Idle==0);
p=Ready(); s=p.Run("baseline check 7"); True(c,"state command samples all four independent weapon reads",p.Liberty.Weapons.Reads==4 && s.Contains("numeric_ready=True gameplay_safe=True") && s.Contains("visual=unproven"));
True(c,"camera and native/owner state exposed without claiming pixels",s.Contains("game_cam_pos=") && s.Contains("game_cam_rot=") && s.Contains("game_cam_fov=60") && s.Contains("native_requested=False native_applied=False") && s.Contains("other_hud_owner=False"));
True(c,"readback refuses extra arguments",p.Run("baseline state 7 extra").StartsWith("error:"));
foreach(string bad in new[]{"baseline idle 7 0","baseline idle 7 120001","baseline aim 7 NaN 1 100","baseline aim 7 25 Infinity 100","baseline aim 7 -1 1 100"}) True(c,"bounded finite task arguments "+bad,p.Run(bad).StartsWith("error:") && p.Liberty.Tasks.Idle==0 && p.Liberty.Tasks.Aim==0);
p.Run("baseline idle 7 15000"); True(c,"idle requests supported task and retains owner",p.Liberty.Tasks.Idle==1 && p.Liberty.Tasks.Duration==15000 && p.baselineTaskPed==p.Liberty.World.Player.Ped);
p.Run("baseline aim 7 25 1 15000"); True(c,"aim clears owned idle and uses heading-derived coordinate",p.Liberty.Tasks.Clears==1 && p.Liberty.Tasks.Aim==1 && p.Liberty.Tasks.Target==new Vec3(10,45,31));
p.Run("baseline off"); p.Run("baseline off"); True(c,"off releases owned task once",p.Liberty.Tasks.Clears==2 && p.baselineTaskPed.IsNone && p.baselineTask=="none");
Action<ProbeModule>[] unsafeStates={x=>x.Liberty.World.Info.Paused=true,x=>x.Liberty.World.Info.CutscenePlaying=true,x=>x.Liberty.World.Info.FadedOut=true,x=>x.Liberty.World.Info.MissionActive=true,x=>x.Liberty.World.Player.HasControl=false,x=>x.Liberty.World.Player.IsDead=true,x=>x.Liberty.World.Player.InVehicle=true,x=>x.Engine.Ui.AnyMenuOpen=true,x=>x.Engine.Ui.OtherOwner=true,x=>x.Liberty.Input.CapturedBy=new Owner(),x=>x.config.Enabled=false,x=>x.probeMode=false,x=>x.layoutTest=true,x=>x.nativeDisplay.Arm(0,100),x=>x.cashTestOriginal=86};
int i=0; foreach(var makeUnsafe in unsafeStates) {
p=Ready(); makeUnsafe(p); True(c,"unsafe task start has no effects "+i,p.Run("baseline idle 7 100").StartsWith("error:") && p.Liberty.Tasks.Idle==0);
p=Ready(); p.Run("baseline idle 7 100"); makeUnsafe(p); p.Tick(); True(c,"unsafe transition clears only owned fixture "+i,p.Liberty.Tasks.Clears==1 && p.baselineTaskPed.IsNone); i++;
}
p=Ready(); p.Run("baseline idle 7 100"); p.config.Enabled=false; p.Tick(); p.config.Enabled=true; p.Tick(); True(c,"config restoration does not rearm task",p.Liberty.Tasks.Idle==1 && p.baselineTaskPed.IsNone);
p=Ready(); p.Run("baseline idle 7 100"); p.Liberty.World.Player.Ped=new PedRef(11); p.Tick(); True(c,"player replacement clears original task owner",p.Liberty.Tasks.Cleared==new PedRef(10));
p=Ready(); p.Run("baseline idle 7 100"); p.Liberty.World.HasPlayer=false; p.Tick(); True(c,"lost player releases fixture owner",p.Liberty.Tasks.Clears==1 && p.baselineTaskPed.IsNone);
p=Ready(); p.Run("baseline idle 7 100"); p.Liberty.Peds.Present=false; p.Release(); True(c,"deleted ped release avoids task native",p.Liberty.Tasks.Clears==0 && p.baselineTaskPed.IsNone);
p=Ready(); p.Run("baseline idle 7 100"); p.Release(); p.Release(); True(c,"shared release path idempotent (not domain unload proof)",p.Liberty.Tasks.Clears==1 && p.baselineTaskPed.IsNone);
p=Ready(); p.clock.ElapsedMilliseconds=500; p.Run("baseline idle 7 100"); p.clock.ElapsedMilliseconds=599; p.Tick(); True(c,"ownership retained strictly before monotonic deadline",p.baselineTaskPed==new PedRef(10) && p.baselineTaskExpiresMilliseconds==600 && p.Liberty.Tasks.Clears==0);
p.clock.ElapsedMilliseconds=600; p.Tick(); True(c,"exact expiry retires owner without clearing native tasks",p.baselineTaskPed.IsNone && p.baselineTask=="none" && p.baselineTaskExpiresMilliseconds==0 && p.Liberty.Tasks.Clears==0);
p.Run("baseline off"); p.Release(); True(c,"off and stop after expiry never clear later work",p.Liberty.Tasks.Clears==0);
p=Ready(); p.Run("baseline idle 7 100"); p.clock.ElapsedMilliseconds=101; p.Run("baseline off"); True(c,"delayed off without tick retires expired owner without native clear",p.baselineTaskPed.IsNone && p.Liberty.Tasks.Clears==0);
p=Ready(); p.Run("baseline aim 7 25 1 100"); p.clock.ElapsedMilliseconds=100; p.Release(); True(c,"delayed stop without tick retires expired aim without native clear",p.baselineTaskPed.IsNone && p.Liberty.Tasks.Clears==0);
p=Ready(); p.Run("baseline idle 7 100"); p.clock.ElapsedMilliseconds=200; p.config.Enabled=false; p.Tick(); True(c,"delayed config-off after expiry does not clear unrelated task",p.baselineTaskPed.IsNone && p.Liberty.Tasks.Clears==0);
p=Ready(); p.Run("baseline idle 7 100"); p.clock.ElapsedMilliseconds=99; p.Liberty.World.Info.Paused=true; p.Tick(); True(c,"pause before expiry still clears owned fixture",p.baselineTaskPed.IsNone && p.Liberty.Tasks.Clears==1);
p=Ready(); p.Run("baseline idle 7 100"); p.clock.ElapsedMilliseconds=150; p.Liberty.World.Info.Paused=true; p.Tick(); True(c,"pause after wall-time expiry retires without asserting native completion",p.baselineTaskPed.IsNone && p.Liberty.Tasks.Clears==0);
p=Ready(); p.Run("baseline idle 7 100"); p.clock.ElapsedMilliseconds=99; p.Release(); True(c,"stop strictly before deadline still clears owned fixture",p.Liberty.Tasks.Clears==1 && p.baselineTaskExpiresMilliseconds==0);
p=Ready(); p.Run("baseline idle 7 100"); p.clock.ElapsedMilliseconds=101; p.Run("baseline aim 7 25 1 100"); True(c,"new fixture after expiry does not clear stale owner",p.Liberty.Tasks.Clears==0 && p.Liberty.Tasks.Aim==1 && p.baselineTaskExpiresMilliseconds==201);
p=Ready(); p.Run("baseline idle 7 100"); p.clock.ElapsedMilliseconds=100; s=p.Run("baseline state 7"); True(c,"readback without tick retires stale task label",s.Contains("task_requested=none") && p.Liberty.Tasks.Clears==0);
Console.WriteLine("focused held-baseline passed="+c.Passed+" failed="+c.Failed); return c.Failed==0?0:1;
}
'''
scenario = (root / 'tools/autopilot/scenarios/hud-held-baseline.txt').read_text()
negative = next(re.match(r'expect "([^"]+)"', line).group(1) for line in scenario.splitlines() if line.startswith('expect "hud_baseline expected=7 held=0'))
positive = next(re.match(r'expect "([^"]+)"', line).group(1) for line in scenario.splitlines() if line.startswith('expect "hud_baseline expected=7 held=7'))
fixture_checks = r'''
var negativePattern=new System.Text.RegularExpressions.Regex(NEGATIVE);
string observed="hud_baseline expected=7 held=0 snapshot=0 owned=True clip=0 total=150 numeric_ready=False gameplay_safe=True";
True(c,"negative scenario accepts actual unarmed clip0 observation",negativePattern.IsMatch(observed));
True(c,"negative scenario does not assume unselected magazine contents",negativePattern.IsMatch(observed.Replace("clip=0", "clip=17")) && negativePattern.IsMatch(observed.Replace("clip=0", "clip=-1")));
foreach(string mismatch in new[]{observed.Replace("held=0", "held=7"),observed.Replace("snapshot=0", "snapshot=7"),observed.Replace("owned=True", "owned=False"),observed.Replace("total=150", "total=0"),observed.Replace("numeric_ready=False", "numeric_ready=True"),observed.Replace("gameplay_safe=True", "gameplay_safe=False")}) True(c,"negative scenario rejects mismatched control "+mismatch,!negativePattern.IsMatch(mismatch));
var positivePattern=new System.Text.RegularExpressions.Regex(POSITIVE);
string ready="hud_baseline expected=7 held=7 snapshot=7 owned=True clip=17 total=150 numeric_ready=True gameplay_safe=True";
True(c,"positive fixture retains selected snapshot and known loaded clip",positivePattern.IsMatch(ready));
foreach(string mismatch in new[]{ready.Replace("clip=17", "clip=0"),ready.Replace("clip=17", "clip=-1"),ready.Replace("held=7", "held=0"),ready.Replace("snapshot=7", "snapshot=0")}) True(c,"positive fixture rejects unready held baseline "+mismatch,!positivePattern.IsMatch(mismatch));
'''.replace('NEGATIVE', json.dumps(negative)).replace('POSITIVE', json.dumps(positive))
tests = tests.replace('Console.WriteLine("focused held-baseline passed=', fixture_checks+'\nConsole.WriteLine("focused held-baseline passed=')
(out/'Focused.cs').write_text(prefix+methods+'\n}\n'+tests+checks+'\n}\n')
print('Generated exact-method focused harness (stubbed game API; no game/runtime evidence).')
