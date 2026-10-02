using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GTA;
using GTA.Native;
using LibertyFramework.CombatEffects.Logic;
using LibertyFramework.Core.Logging;
using LibertyFramework.GameApi;

namespace LibertyFramework.CombatEffects
{
    // T-022 dismemberment. A severed part = a bone and all its descendants collapsed into the cut joint (tiny uniform
    // scale, origin at the joint), so the mesh vanishes there and the surrounding skin closes over the stump.
    // The collapse is applied (1) inside the engine after every skeleton pose update, by SkeletonCollapseEngine
    // (ADR-0005), for the ped's own matrices and the frag cache entry's copy, and (2) on every script tick as a
    // fallback. A thrown limb is a clone of the ped with everything except that limb collapsed.
    internal sealed class Dismemberment
    {
        private sealed class Collapse
        {
            internal Ped Ped;
            internal bool Clone;
            internal string Name;
            internal int CutIndex;
            internal int[] Indices;
            internal int BoneCount;
            internal uint FragInst, Skeleton, Matrices, CopyMatrices;
            internal int CopyBoneCount;
            internal long CreatedMilliseconds, LifetimeMilliseconds;
            internal int Ticks;
            internal bool EvidenceLogged, Shown, LimbThrown;
            internal Vector3 Push;
            internal int StumpTag;
            internal int CutTag;
            internal int HitsAtCreate;
            internal bool Pinned;
            internal int ConfirmTicks;
            internal bool Hidden, Settled;
            internal long ShownMilliseconds;
            internal int ThrowAttempts;
            internal long NextThrowMilliseconds;
            internal bool LandingShown;
        }

        private readonly PedSkeleton skeleton;
        private readonly SkeletonCollapseEngine engine;
        private readonly float collapseScale;
        private readonly List<Collapse> records = new List<Collapse>();
        private bool tableDirty;
        private bool variationFailureLogged;

        internal Dismemberment(PedSkeleton skeleton, SkeletonCollapseEngine engine, float collapseScale)
        {
            this.skeleton = skeleton;
            this.engine = engine;
            this.collapseScale = collapseScale;
        }

        internal bool EngineActive { get { return engine != null && engine.PatchCount > 0; } }

        // Corpse cuts already made on this ped (thrown-limb clones excluded).
        internal int CutsOn(Ped ped)
        {
            int count = 0;
            foreach (Collapse record in records) { if (record.Ped == ped && !record.Clone) { count++; } }
            return count;
        }

        internal bool IsTracked(Ped ped)
        {
            foreach (Collapse record in records) { if (record.Ped == ped) { return true; } }
            return false;
        }

        internal bool IsTracked(Ped ped, string name)
        {
            foreach (Collapse record in records) { if (record.Ped == ped && record.Name == name && !record.Clone) { return true; } }
            return false;
        }

        // False when the skeleton cannot be read safely (logged).
        internal bool Sever(Ped target, LimbCutPlan plan, Vector3 push, long now, long lifetime)
        {
            if (IsTracked(target, plan.Name)) { return false; }
            Collapse record = Build(target, plan.CutTag, plan.Name, false, null);
            if (record == null) { return false; }
            record.Push = push;
            record.StumpTag = plan.StumpTag;
            record.CutTag = plan.CutTag;
            record.CreatedMilliseconds = now;
            record.LifetimeMilliseconds = lifetime;
            record.HitsAtCreate = engine != null ? engine.Hits : 0;
            // Playtest 2: the corpse vanished right after the thrown limb was spawned (the population manager frees a
            // ped slot by removing ambient corpses). A mission-owned corpse is kept; it is released when the record ends.
            try { Function.Call("SET_CHAR_AS_MISSION_CHAR", target); record.Pinned = true; }
            catch (Exception error) { RuntimeLog.Error("dismember_pin_failed error=" + error.Message); }
            records.Add(record);
            tableDirty = true;
            Apply(record);
            RuntimeLog.Info("dismember part=" + plan.Name + " bones=" + record.Indices.Length + " skeleton_bones=" + record.BoneCount +
                " engine=" + EngineActive + " copy=" + (record.CopyMatrices != 0));
            return true;
        }

        private Collapse Build(Ped ped, int cutTag, string name, bool clone, int[] keep)
        {
            uint pointer = skeleton.PedFromHandle(ped.GetHashCode());
            if (pointer == 0) { RuntimeLog.Error("dismember_skip no_ped_pointer part=" + name); return null; }
            int cut = skeleton.IndexOf(pointer, ped.Model.Hash, cutTag);
            if (cut <= 0) { RuntimeLog.Error("dismember_skip cut_bone_unresolved part=" + name + " tag=0x" + cutTag.ToString("X")); return null; }
            Collapse record = new Collapse();
            record.Ped = ped; record.Clone = clone; record.Name = name; record.CutIndex = cut;
            if (!Refresh(record, pointer)) { RuntimeLog.Error("dismember_skip no_matrices part=" + name); return null; }
            if (record.BoneCount <= cut) { RuntimeLog.Error("dismember_skip bone_count=" + record.BoneCount + " part=" + name); return null; }
            List<int> indices = record.Skeleton != 0 ? skeleton.Subtree(record.Skeleton, cut, record.BoneCount) : new List<int>();
            if (indices.Count == 0) { indices.Add(cut); }
            if (keep != null)
            {
                // Clone: collapse everything except the kept limb.
                List<int> rest = new List<int>();
                for (int index = 1; index < record.BoneCount; index++) { if (Array.IndexOf(keep, index) < 0) { rest.Add(index); } }
                indices = rest;
            }
            if (indices.Count > SkeletonCollapseEngine.MaximumIndices) { RuntimeLog.Error("dismember_skip too_many_bones=" + indices.Count); return null; }
            record.Indices = indices.ToArray();
            return record;
        }

        // Re-reads the engine pointers (ragdoll on/off can swap them). True when a matrix array is known.
        private bool Refresh(Collapse record, uint pointer)
        {
            uint frag = skeleton.FragInst(pointer);
            uint skel = frag != 0 ? skeleton.Skeleton(frag) : 0;
            uint matrices = skel != 0 ? skeleton.MatricesOf(skel) : 0;
            uint expected = skeleton.MatrixBase(pointer);
            if (matrices == 0 || matrices != expected) { skel = 0; matrices = expected; }
            if (matrices == 0) { return false; }
            int count = skel != 0 ? skeleton.BoneCountOf(skel) : (record.BoneCount > 0 ? record.BoneCount : skeleton.BoneCount(pointer));
            uint copySkeleton = frag != 0 ? skeleton.CacheCopySkeleton(frag) : 0;
            uint copy = copySkeleton != 0 ? skeleton.MatricesOf(copySkeleton) : 0;
            int copyCount = copy != 0 ? skeleton.BoneCountOf(copySkeleton) : 0;
            if (copy == matrices || copyCount != count) { copy = 0; copyCount = 0; }
            if (frag != record.FragInst || skel != record.Skeleton || matrices != record.Matrices || copy != record.CopyMatrices ||
                (record.BoneCount != 0 && count != record.BoneCount))
            {
                tableDirty = true;
            }
            record.FragInst = frag; record.Skeleton = skel; record.Matrices = matrices;
            record.CopyMatrices = copy; record.CopyBoneCount = copyCount;
            if (count > 0) { record.BoneCount = count; }
            return true;
        }

        // Script-tick fallback: the same write the native routine does.
        private void Apply(Collapse record)
        {
            WriteCollapse(record.Matrices, record);
            if (record.CopyMatrices != 0) { WriteCollapse(record.CopyMatrices, record); }
        }

        private void WriteCollapse(uint matrices, Collapse record)
        {
            IntPtr cut = new IntPtr((int)(matrices + (uint)(record.CutIndex * 64) + 48));
            float[] origin = new float[3];
            Marshal.Copy(cut, origin, 0, 3);
            float[] rows = { collapseScale, 0, 0, 0, 0, collapseScale, 0, 0, 0, 0, collapseScale };
            foreach (int index in record.Indices)
            {
                IntPtr m = new IntPtr((int)(matrices + (uint)(index * 64)));
                Marshal.Copy(rows, 0, m, 3);
                Marshal.Copy(rows, 4, IntPtr.Add(m, 16), 3);
                Marshal.Copy(rows, 8, IntPtr.Add(m, 32), 3);
                Marshal.Copy(origin, 0, IntPtr.Add(m, 48), 3);
            }
        }

        private void PublishTable()
        {
            tableDirty = false;
            if (engine == null) { return; }
            List<SkeletonCollapseEngine.Entry> entries = new List<SkeletonCollapseEngine.Entry>();
            foreach (Collapse record in records)
            {
                if (record.Skeleton == 0) { continue; } // bone count unknown to the engine: tick fallback only
                entries.Add(Entry(record.Matrices, record.BoneCount, record));
                if (record.CopyMatrices != 0) { entries.Add(Entry(record.CopyMatrices, record.CopyBoneCount, record)); }
            }
            engine.Publish(entries);
        }

        private static SkeletonCollapseEngine.Entry Entry(uint matrices, int boneCount, Collapse record)
        {
            SkeletonCollapseEngine.Entry entry = new SkeletonCollapseEngine.Entry();
            entry.Matrices = matrices; entry.BoneCount = boneCount; entry.CutIndex = record.CutIndex; entry.Indices = record.Indices;
            return entry;
        }

        // Per-tick upkeep (T-022 rework after playtest: floating legs, limbs flashing the whole NPC).
        // - Skeleton moves are detected every tick with the engine's own CPed::BoneMatrix (no VirtualQuery); only a
        //   changed pointer triggers the full Refresh and a table republish.
        // - A thrown-limb clone stays invisible until the engine collapse is confirmed live on its current skeleton for
        //   limbConfirmTicks ticks; if its skeleton moves later (ragdoll start/end) it is hidden again until reconfirmed.
        // - A limb still resting above the ground (propped on its own invisible body) after limbSettleMilliseconds is
        //   removed instead of floating.
        internal void Update(CombatEffectsConfig config, long now, Func<object, bool> onThrowReady, Action<Ped, int> onLanding)
        {
            List<Collapse> readyToThrow = new List<Collapse>();
            for (int i = records.Count - 1; i >= 0; i--)
            {
                Collapse record = records[i];
                bool exists = record.Ped != null && Natives.PedExists(record.Ped);
                if (!exists || now - record.CreatedMilliseconds > record.LifetimeMilliseconds)
                {
                    if (!exists && !record.Clone && now - record.CreatedMilliseconds < 5000)
                    {
                        RuntimeLog.Error("dismember_corpse_lost part=" + record.Name + " age_ms=" + (now - record.CreatedMilliseconds) + " ticks=" + record.Ticks);
                    }
                    if (record.Clone && exists) { record.Ped.Delete(); }
                    else if (record.Pinned && exists) { record.Ped.NoLongerNeeded(); }
                    records.RemoveAt(i);
                    tableDirty = true;
                    continue;
                }
                uint pointer = skeleton.PedFromHandle(record.Ped.GetHashCode());
                if (pointer == 0) { continue; }
                uint current = skeleton.MatrixPointerFast(pointer);
                if (current != record.Matrices || record.Ticks == 0)
                {
                    if (!Refresh(record, pointer)) { continue; }
                    tableDirty = true;
                    if (record.Clone)
                    {
                        record.ConfirmTicks = 0;
                        if (record.Shown) { record.Ped.Visible = false; record.Hidden = true; RuntimeLog.Info("dismember_limb_rehidden part=" + record.Name); }
                    }
                }
                Apply(record);
                record.Ticks++;
                if (record.Clone) { UpdateClone(config, record, pointer, now, onLanding); if (!records.Contains(record)) { continue; } }
                if (!record.Clone && !record.LimbThrown && record.Ticks >= 2 && record.Name != "head" &&
                    onThrowReady != null && now >= record.NextThrowMilliseconds) readyToThrow.Add(record);
                if (!record.EvidenceLogged && record.Ticks >= 30)
                {
                    record.EvidenceLogged = true;
                    RuntimeLog.Info("dismember_evidence part=" + record.Name + " clone=" + record.Clone + " ticks=" + record.Ticks +
                        " engine_hits=" + (engine != null ? engine.Hits - record.HitsAtCreate : 0) + " engine_calls=" + (engine != null ? engine.Calls : 0) +
                        " skeleton=" + (record.Skeleton != 0) + " copy=" + (record.CopyMatrices != 0));
                }
            }
            if (tableDirty) { PublishTable(); }
            // Spawn only after iteration: replacing the oldest thrown limb can remove a record from this list.
            foreach (Collapse record in readyToThrow)
            {
                if (!records.Contains(record)) continue;
                record.ThrowAttempts++;
                if (onThrowReady(record)) { record.LimbThrown = true; continue; }
                if (record.ThrowAttempts >= config.LimbThrowMaximumAttempts)
                {
                    record.LimbThrown = true;
                    RuntimeLog.Error("dismember_limb_gave_up part=" + record.Name + " attempts=" + record.ThrowAttempts);
                }
                else { record.NextThrowMilliseconds = now + config.LimbThrowRetryMilliseconds; }
            }
        }

        private void UpdateClone(CombatEffectsConfig config, Collapse record, uint pointer, long now, Action<Ped, int> onLanding)
        {
            // Confirmation: the engine applied our entry on this skeleton since the last publish (or, without the
            // engine, our own per-tick write has run), for limbConfirmTicks consecutive ticks.
            bool live = engine == null || !EngineActive || record.Skeleton == 0 || engine.HitsFor(record.Matrices) > 0;
            record.ConfirmTicks = live ? record.ConfirmTicks + 1 : 0;
            if (record.ConfirmTicks < Math.Max(1, config.LimbConfirmTicks)) { return; }
            if (!record.Shown)
            {
                record.Shown = true;
                record.ShownMilliseconds = now;
                record.Ped.Visible = true;
                try
                {
                    Function.Call("APPLY_FORCE_TO_PED", record.Ped, 3, record.Push.X * config.SeveredLimbForce, record.Push.Y * config.SeveredLimbForce,
                        config.SeveredLimbForce * config.SeveredLimbVerticalForceFraction, 0.0f, 0.0f, 0.0f, 0, 1, 1, 1);
                }
                catch (Exception error) { RuntimeLog.Error("dismember_limb_force_failed error=" + error.Message); }
                RuntimeLog.Info("dismember_limb_visible part=" + record.Name + " confirm_ticks=" + record.ConfirmTicks);
                return;
            }
            if (record.Hidden)
            {
                record.Hidden = false;
                record.Ped.Visible = true;
                RuntimeLog.Info("dismember_limb_reshown part=" + record.Name);
            }
            if (record.Settled || now - record.ShownMilliseconds < config.LimbSettleMilliseconds) { return; }
            record.Settled = true;
            try
            {
                float[] joint = skeleton.WorldPosition(pointer, record.CutTag);
                if (joint == null) { return; }
                float height = joint[2] - Natives.GroundZ(joint[0], joint[1], joint[2] + 0.5f);
                if (height > config.LimbFloatingHeightMeters)
                {
                    RuntimeLog.Info("dismember_limb_floating_removed part=" + record.Name + " height=" + height.ToString("0.00"));
                    if (onLanding != null) { onLanding(record.Ped, record.CutTag); }
                    record.Ped.Delete();
                    records.Remove(record);
                    tableDirty = true;
                    return;
                }
                if (!record.LandingShown && onLanding != null)
                {
                    record.LandingShown = true;
                    onLanding(record.Ped, record.CutTag);
                }
                RuntimeLog.Info("dismember_limb_landed part=" + record.Name + " height=" + height.ToString("0.00"));
            }
            catch (Exception error) { RuntimeLog.Error("dismember_limb_settle_failed error=" + error.Message); }
        }

        // Spawn the thrown limb for a severed corpse: same model and clothes, every bone but the limb collapsed.
        // Everything read from the corpse is read first, so a corpse the game removes mid-way cannot throw.
        internal bool ThrowLimb(CombatEffectsConfig config, object severedRecord, long now)
        {
            Collapse source = (Collapse)severedRecord;
            if (source.Ped == null || !source.Ped.Exists()) { return false; }
            if (CountClones() >= config.MaximumSeveredPeds && !ReplaceOldestClone()) { return false; }
            Model model; Vector3 position; float heading;
            try { model = source.Ped.Model; position = source.Ped.Position; heading = source.Ped.Heading; }
            catch (Exception error) { RuntimeLog.Error("dismember_limb_source_failed error=" + error.Message); return false; }
            int[] drawables = new int[11], textures = new int[11];
            bool clothes = true;
            try
            {
                for (int component = 0; component < 11; component++)
                {
                    drawables[component] = Function.Call<int>("GET_CHAR_DRAWABLE_VARIATION", source.Ped, component);
                    textures[component] = Function.Call<int>("GET_CHAR_TEXTURE_VARIATION", source.Ped, component);
                }
            }
            catch (Exception error)
            {
                clothes = false;
                if (!variationFailureLogged) { variationFailureLogged = true; RuntimeLog.Error("dismember_limb_clothes_skipped error=" + error.Message); }
            }
            Ped clone = null;
            try
            {
                // Beside the corpse (along the shot), on the ground: an invisible clone body lying on the corpse is what
                // held limbs up in the air in playtest.
                Vector3 spawn = position + new Vector3(source.Push.X * config.SeveredLimbSpawnOffsetMeters, source.Push.Y * config.SeveredLimbSpawnOffsetMeters, 0);
                float ground = Natives.GroundZ(spawn.X, spawn.Y, spawn.Z + 1.0f);
                spawn.Z = (ground > spawn.Z - 3.0f && ground < spawn.Z + 3.0f ? ground : spawn.Z) + config.SeveredLimbSpawnHeightMeters;
                clone = World.CreatePed(model, spawn);
                if (clone == null || !clone.Exists()) { RuntimeLog.Error("dismember_limb_spawn_failed"); return false; }
                clone.Visible = false;
                if (clothes) { for (int component = 0; component < 11; component++) Function.Call("SET_CHAR_COMPONENT_VARIATION", clone, component, drawables[component], textures[component]); }
                clone.Heading = heading;
                Function.Call("SET_CHAR_AS_MISSION_CHAR", clone);
                clone.Die();
                Collapse record = Build(clone, source.CutTag, source.Name + "_limb", true, source.Indices);
                if (record == null) { clone.Delete(); return false; }
                record.CutIndex = source.CutIndex;
                record.StumpTag = source.CutTag;
                record.Push = source.Push;
                record.CreatedMilliseconds = now;
                record.LifetimeMilliseconds = config.SeveredLimbLifetimeMilliseconds;
                record.HitsAtCreate = engine != null ? engine.Hits : 0;
                Apply(record);
                records.Add(record);
                tableDirty = true;
                RuntimeLog.Info("dismember_limb_thrown part=" + source.Name + " collapsed=" + record.Indices.Length);
                return true;
            }
            catch (Exception error)
            {
                RuntimeLog.Error("dismember_limb_spawn_failed error=" + error.Message);
                try { if (clone != null && clone.Exists()) clone.Delete(); }
                catch (Exception cleanup) { RuntimeLog.Error("dismember_limb_cleanup_failed error=" + cleanup.Message); }
                return false;
            }
        }

        private bool ReplaceOldestClone()
        {
            Collapse oldest = null;
            foreach (Collapse record in records)
                if (record.Clone && (oldest == null || record.CreatedMilliseconds < oldest.CreatedMilliseconds)) oldest = record;
            if (oldest == null) return false;
            try { if (oldest.Ped != null && oldest.Ped.Exists()) oldest.Ped.Delete(); }
            catch (Exception error) { RuntimeLog.Error("dismember_limb_replace_failed error=" + error.Message); return false; }
            records.Remove(oldest);
            tableDirty = true;
            RuntimeLog.Info("dismember_limb_replaced_oldest part=" + oldest.Name);
            return true;
        }

        private int CountClones()
        {
            int count = 0;
            foreach (Collapse record in records) { if (record.Clone) { count++; } }
            return count;
        }

        internal int SeveredCount
        {
            get
            {
                HashSet<Ped> corpses = new HashSet<Ped>();
                foreach (Collapse record in records) { if (!record.Clone) corpses.Add(record.Ped); }
                return corpses.Count;
            }
        }

        internal void Clear()
        {
            if (engine != null) { engine.Publish(new List<SkeletonCollapseEngine.Entry>()); }
            foreach (Collapse record in records)
            {
                try
                {
                    if (record.Ped == null || !record.Ped.Exists()) { continue; }
                    if (record.Clone) { record.Ped.Delete(); } else if (record.Pinned) { record.Ped.NoLongerNeeded(); }
                }
                catch (Exception error) { RuntimeLog.Error("dismember_clear_failed error=" + error.Message); }
            }
            records.Clear();
        }
    }
}
