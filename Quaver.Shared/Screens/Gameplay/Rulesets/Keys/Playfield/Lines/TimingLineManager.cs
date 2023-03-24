/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 * Copyright (c) Swan & The Quaver Team <support@quavergame.com>.
*/

using System;
using System.Linq;
using System.Collections.Generic;
using Quaver.API.Enums;
using Quaver.API.Helpers;
using Quaver.API.Maps;
using Quaver.Shared.Config;
using Quaver.Shared.Screens.Gameplay.Rulesets.Keys.HitObjects;
using System.Collections.Concurrent;

namespace Quaver.Shared.Screens.Gameplay.Rulesets.Keys.Playfield.Lines
{
    public class TimingLineManager
    {
        /// <summary>
        ///     Timing Line object pool.
        /// </summary>
        private ConcurrentBag<TimingLine> Pool { get; set; }

        /// <summary>
        ///     Spatial hash map allowing for quickly determining timing lines close to the receptors
        /// </summary>
		private SpatialHashMap<TimingLineInfo> SpatialHashMap { get; set; }

        /// <summary>
        ///     List of timing lines that are being rendered to screen
        /// </summary>
		private HashSet<TimingLineInfo> RenderedLineInfos { get; set; }

        /// <summary>
        ///     Reference to the HitObjectManager
        /// </summary>
        public HitObjectManagerKeys HitObjectManager { get; }

        /// <summary>
        ///     Reference to the current Ruleset
        /// </summary>
        public GameplayRulesetKeys Ruleset { get; }

        /// <summary>
        ///     Initial size for the object pool
        /// </summary>
        private int InitialPoolSize { get; } = 6;

        /// <summary>
        ///     The Scroll Direction of every Timing Line
        /// </summary>
        public ScrollDirection ScrollDirection { get; }

        /// <summary>
        ///     Target position when TrackPosition = 0
        /// </summary>
        private float TrackOffset { get; }

        /// <summary>
        ///     Size of every Timing Line
        /// </summary>
        private float SizeX { get; }

        /// <summary>
        ///     Position of every Timing Line
        /// </summary>
        private float PositionX { get; }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="map"></param>
        /// <param name="ruleset"></param>
        public TimingLineManager(GameplayRulesetKeys ruleset, ScrollDirection direction, float targetY, float size, float offset)
        {
            TrackOffset = targetY;
            SizeX = size;
            PositionX = offset;
            ScrollDirection = direction;
            Ruleset = ruleset;
            HitObjectManager = (HitObjectManagerKeys)ruleset.HitObjectManager;
            GenerateTimingLineInfo(ruleset.Map);
            InitializeObjectPool();
        }

        /// <summary>
        ///     Generate Timing Line Information for the map
        /// </summary>
        /// <param name="map"></param>
        public void GenerateTimingLineInfo(Qua map)
        {
            // Using cell size equal to render area guarantees a consistent two cells accessed per update
            SpatialHashMap = new SpatialHashMap<TimingLineInfo>(HitObjectManager.RenderThreshold * 2);

            // Generate timing line info
            for (var i = 0; i < map.TimingPoints.Count; i++)
            {
                if (map.TimingPoints[i].Hidden)
                    continue;

                // Get target position and increment
                // Target position has tolerance of 1ms so timing points dont overlap by chance
                var target = i + 1 < map.TimingPoints.Count ? map.TimingPoints[i + 1].StartTime - 1 : map.Length;

                var signature = (int)map.TimingPoints[i].Signature;

                // Max possible sane value for timing lines
                const float maxBpm = 9999f;

                var msPerBeat = 60000 / Math.Min(Math.Abs(map.TimingPoints[i].Bpm), maxBpm);
                var increment = signature * msPerBeat;

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (increment <= 0)
                    continue;

                // Initialize timing lines between current timing point and target position
                for (var songPos = map.TimingPoints[i].StartTime; songPos < target; songPos += increment)
                {
                    var offset = HitObjectManager.GetPositionFromTime(songPos);
					SpatialHashMap.Add(offset, new TimingLineInfo(songPos, offset));
                }
            }
        }

        /// <summary>
        ///     Initialize the Timing Line Object Pool
        /// </summary>
        public void InitializeObjectPool()
        {
            Pool = new ConcurrentBag<TimingLine>();
            RenderedLineInfos = new HashSet<TimingLineInfo>();

            for (int i = 0; i < InitialPoolSize; i++)
			{
				Pool.Add(new TimingLine(Ruleset, ScrollDirection, TrackOffset, SizeX, PositionX));
			}
        }

        /// <summary>
        ///     Determine which timing lines to render and update sprite positions
        /// </summary>
        public void UpdateTimingLines()
        {
            // stop rendering lines that exited the range
            bool shouldRemove(TimingLineInfo info)
            {
                if (!TimingLineInRange(info))
                {
                    Pool.Add(info.Unlink());
                    return true;
                }

                return false;
            }

            RenderedLineInfos.RemoveWhere(info => shouldRemove(info));

            // start rendering lines that entered the range
            var inRangeLines = new HashSet<TimingLineInfo>();
            inRangeLines.UnionWith(SpatialHashMap.GetValues(HitObjectManager.CurrentTrackPosition - HitObjectManager.RenderThreshold));
            inRangeLines.UnionWith(SpatialHashMap.GetValues(HitObjectManager.CurrentTrackPosition + HitObjectManager.RenderThreshold));
            inRangeLines.RemoveWhere(info => info.Line != null || !TimingLineInRange(info));

            foreach (var info in inRangeLines)
			{
				TimingLine line;
				if (!Pool.TryTake(out line))
					line = new TimingLine(Ruleset, ScrollDirection, TrackOffset, SizeX, PositionX);

				info.Link(line);
				RenderedLineInfos.Add(info);
			}

            // update sprite positions
			foreach (var info in RenderedLineInfos)
			{
				info.Line.UpdateSpritePosition(HitObjectManager.CurrentTrackPosition);
			}
        }

        private bool TimingLineInRange(TimingLineInfo info)
        {
            return Math.Abs(info.TrackOffset - HitObjectManager.CurrentTrackPosition) <= HitObjectManager.RenderThreshold;
        }
    }
}
