using Quaver.API.Enums;
using Quaver.API.Helpers;
using Quaver.API.Maps;
using Quaver.API.Maps.Processors.Difficulty.Optimization;
using Quaver.API.Maps.Processors.Difficulty.Rulesets.Keys.Structures;
using Quaver.API.Maps.Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quaver.API.Maps.Processors.Difficulty.Rulesets.Keys
{
    /// <summary>
    ///     Will be used to solve Strain Rating.
    /// </summary>
    public class StrainSolverKeys : StrainSolver
    {
        /// <summary>
        ///     Constants used for solving
        /// </summary>
        public StrainConstantsKeys StrainConstants { get; private set; }

        /// <summary>
        ///     Average note density of the map
        /// </summary>
        public float AverageNoteDensity { get; private set; } = 0;

        /// <summary>
        ///     Assumes that the assigned hand will be the one to press that key
        /// </summary>
        public static Dictionary<int, Hand> LaneToHand4K { get;} = new Dictionary<int, Hand>()
        {
            { 1, Hand.Left },
            { 2, Hand.Left },
            { 3, Hand.Right },
            { 4, Hand.Right }
        };

        /// <summary>
        ///     Assumes that the assigned hand will be the one to press that key
        /// </summary>
        public static Dictionary<int, Hand> LaneToHand7K { get; } = new Dictionary<int, Hand>()
        {
            { 1, Hand.Left },
            { 2, Hand.Left },
            { 3, Hand.Left },
            { 4, Hand.Ambiguous },
            { 5, Hand.Right },
            { 6, Hand.Right },
            { 7, Hand.Right }
        };

        /// <summary>
        ///     Assumes that the assigned finger will be the one to press that key.
        /// </summary>
        public static Dictionary<int, FingerState> LaneToFinger4K { get; } = new Dictionary<int, FingerState>()
        {
            { 1, FingerState.Middle },
            { 2, FingerState.Index },
            { 3, FingerState.Index },
            { 4, FingerState.Middle }
        };

        /// <summary>
        ///     Assumes that the assigned finger will be the one to press that key.
        /// </summary>
        public static Dictionary<int, FingerState> LaneToFinger7K { get; } = new Dictionary<int, FingerState>()
        {
            { 1, FingerState.Ring },
            { 2, FingerState.Middle },
            { 3, FingerState.Index },
            { 4, FingerState.Thumb },
            { 5, FingerState.Index },
            { 6, FingerState.Middle },
            { 7, FingerState.Ring }
        };

        /// <summary>
        ///     Value of confidence that there's vibro manipulation in the calculated map.
        /// </summary>
        private float VibroInaccuracyConfidence { get; set; }

        /// <summary>
        ///     Value of confidence that there's roll manipulation in the calculated map.
        /// </summary>
        private float RollInaccuracyConfidence { get; set; }

        /// <summary>
        ///     Solves the difficulty of a .qua file
        /// </summary>
        /// <param name="map"></param>
        /// <param name="constants"></param>
        /// <param name="mods"></param>
        /// <param name="detailedSolve"></param>
        public StrainSolverKeys(Qua map, StrainConstants constants, ModIdentifier mods = ModIdentifier.None, bool detailedSolve = false) : base(map, constants, mods)
        {
            // Cast the current Strain Constants Property to the correct type.
            StrainConstants = (StrainConstantsKeys)constants;

            // Don't bother calculating map difficulty if there's less than 2 hit objects
            if (map.HitObjects.Count < 2)
                return;

            // Solve for difficulty
            CalculateDifficulty(mods);

            // If detailed solving is enabled, expand calculation
            if (detailedSolve)
            {
                // ComputeNoteDensityData();
                //ComputeForPatternFlags();
            }
        }

        /// <summary>
        ///     Calculate difficulty of a map with given rate
        /// </summary>
        /// <param name="rate"></param>
        public void CalculateDifficulty(ModIdentifier mods)
        {
            // If map does not exist, ignore calculation.
            if (Map == null) return;

            // Get song rate from selected mods
            var rate = ModHelper.GetRateFromMods(mods);

            // Compute for overall difficulty
            switch (Map.Mode)
            {
                case (GameMode.Keys4):
                    OverallDifficulty = ComputeForOverallDifficulty(rate);
                    break;
                case (GameMode.Keys7):
                    OverallDifficulty = (ComputeForOverallDifficulty(rate, Hand.Left) + ComputeForOverallDifficulty(rate, Hand.Right)) / 2;
                    break;
            }
        }

        /// <summary>
        ///     Calculate overall difficulty of a map. "AssumeHand" is used for odd-numbered keymodes.
        /// </summary>
        /// <param name="rate"></param>
        /// <param name="assumeHand"></param>
        /// <returns></returns>
        private float ComputeForOverallDifficulty(float rate, Hand assumeHand = Hand.Right)
        {
            // Convert to hitobjects
            var hitObjects = ConvertToStrainHitObject(assumeHand);
            var leftHandData = new List<HandStateData>();
            var rightHandData = new List<HandStateData>();
            var allHandData = new List<HandStateData>();
            var wristStateData = new List<WristState>();

            // Get Initial Handstates
            // - Iterate through hit objects backwards
            hitObjects.Reverse();
            List<HandStateData> refHandData;
            for (var i = 0; i < hitObjects.Count; i++)
            {
                // Determine Reference Hand
                switch (Map.Mode)
                {
                    case GameMode.Keys4:
                        if (LaneToHand4K[hitObjects[i].HitObject.Lane] == Hand.Left)
                            refHandData = leftHandData;
                        else
                            refHandData = rightHandData;
                        break;
                    case GameMode.Keys7:
                        var hand = LaneToHand7K[hitObjects[i].HitObject.Lane];
                        if (hand.Equals(Hand.Left) || (hand.Equals(Hand.Ambiguous) && assumeHand.Equals(Hand.Left)))
                            refHandData = leftHandData;
                        else
                            refHandData = rightHandData;
                        break;
                    default:
                        throw new Exception("Unknown GameMode");
                }

                // Iterate through established handstates for chords
                var chordFound = false;
                for (var j = 0; j < refHandData.Count; j++)
                {
                    // Break loop after leaving threshold
                    if (refHandData[j].Time
                        > hitObjects[i].StartTime + HandStateData.CHORD_THRESHOLD_SAMEHAND_MS)
                        break;

                    // Check for finger overlap
                    chordFound = true;
                    for (var k = 0; k < refHandData[j].HitObjects.Count; k++)
                    {
                        if (refHandData[j].HitObjects[k].HitObject.Lane == hitObjects[i].HitObject.Lane)
                        {
                            chordFound = false;
                            break;
                        }
                    }

                    // Add HitObject to Chord if no fingers overlap
                    if (chordFound)
                    {
                        refHandData[j].AddHitObjectToChord(hitObjects[i]);
                        break;
                    }
                }

                // Add new HandStateData to list if no chords are found
                if (!chordFound)
                {
                    refHandData.Add(new HandStateData(hitObjects[i]));
                    allHandData.Add(refHandData.Last());
                }

                //Console.WriteLine(chordFound);
            }

            // Compute for chorded pairs
            for (var i=0; i<allHandData.Count; i++)
            {
                for (var j=i+1; j<allHandData.Count; j++)
                {
                    if (allHandData[i].Time - allHandData[j].Time > HandStateData.CHORD_THRESHOLD_OTHERHAND_MS)
                    {
                        break;
                    }

                    if (allHandData[j].ChordedHand == null)
                    {
                        if (!allHandData[i].Hand.Equals(allHandData[j].Hand) && !allHandData[j].Hand.Equals(Hand.Ambiguous))
                        {
                            allHandData[j].ChordedHand = allHandData[i];
                            allHandData[i].ChordedHand = allHandData[j];
                            break;
                        }
                    }
                }
            }

            // Compute for wrist action
            // maybe solve this first?
            WristState laterStateLeft = null;
            WristState laterStateRight = null;
            WristState wrist;
            FingerState state;
            for (var i = 0; i < hitObjects.Count; i++)
            {
                if (hitObjects[i].WristState == null)
                {
                    state = hitObjects[i].FingerState;
                    if (hitObjects[i].Hand == Hand.Left)
                    {
                        wrist = new WristState(laterStateLeft);
                        laterStateLeft = wrist;
                    }
                    else
                    {
                        wrist = new WristState(laterStateRight);
                        laterStateRight = wrist;
                    }

                    for (var j = i + 1; j < hitObjects.Count; j++)
                    {
                        if (hitObjects[j].Hand == hitObjects[i].Hand)
                        {
                            // Break loop upon same finger found
                            if (((int)state & (1 << (int)hitObjects[j].FingerState - 1)) != 0)
                                break;

                            state |= hitObjects[j].FingerState;
                            hitObjects[j].WristState = wrist;
                        }
                    }

                    wrist.WristPair = state;
                    wrist.Time = hitObjects[i].StartTime;

                    // check if wrist state is the same
                    if (!state.Equals(hitObjects[i].FingerState))
                    {
                        wrist.WristAction = WristAction.Up;
                        hitObjects[i].WristState = wrist;
                        //Console.WriteLine($"asd{state}_ {hitObjects[i].FingerState} | {Map.Length}, {hitObjects[i].StartTime}");
                    }
                    // same state jack
                    else if (wrist.NextState != null && wrist.NextState.WristPair.Equals(state))
                    {
                        wrist.WristAction = WristAction.Up;
                        hitObjects[i].WristState = wrist;
                    }
                    else
                    {
                        wrist = null;
                    }
                }
                //Console.WriteLine(hitObjects[i].WristState == null);
            }

            // temp calc variables
            var count = 0;
            float currentDiff = 1;
            float total = 0;

            // TEST CALC (OLD)
            for (var z = 0; z <= 1; z++)
            {
                if (z == 0)
                {
                    refHandData = leftHandData;
                }
                else
                {
                    refHandData = rightHandData;
                }

                //todo: this is a temp const variable for graph
                currentDiff = 0;
                for (var i = 0; i < refHandData.Count - 2; i++)
                {
                    refHandData[i].EvaluateDifficulty(StrainConstants);
                    // lean towards more diff sections if its within a certain time range
                    if (refHandData[i].StateDifficulty < currentDiff)
                    {
                        currentDiff += (refHandData[i].StateDifficulty - currentDiff)
                            * StrainConstants.StaminaDecrementalMultiplier.Value * Math.Min((refHandData[i].Time - refHandData[i + 2].Time) / 1000f, 1);
                    }
                    else
                    {
                        currentDiff += (refHandData[i].StateDifficulty - currentDiff)
                            * StrainConstants.StaminaIncrementalMultiplier.Value * Math.Min((refHandData[i].Time - refHandData[i + 2].Time / 1000f), 1);
                    }
                    // todo: create method to interpolate bpm?
                    if ((refHandData[i].Time
                        - refHandData[i + 2].Time) != 0)
                    {
                        count++;
                        total
                            += Math.Max(1, refHandData[i].StateDifficulty
                            * StrainConstants.DifficultyMultiplier
                            * (float)Math.Sqrt(30000 / (refHandData[i].Time - refHandData[i + 2].Time)) + StrainConstants.DifficultyOffset);
                    }
                }
            }

            // temp diff
            var stam = (float)(Math.Log10(count) / 25 + 0.9);
            if (count == 0) return 0;
            return stam * total / count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="assumeHand"></param>
        /// <returns></returns>
        private List<StrainSolverHitObject> ConvertToStrainHitObject(Hand assumeHand)
        {
            var hitObjects = new List<StrainSolverHitObject>();
            foreach (var ho in Map.HitObjects)
                hitObjects.Add(new StrainSolverHitObject(ho, Map.Mode, assumeHand));

            return hitObjects;
        }

        /*
        /// <summary>
        ///     Get Note Data, and compute the base strain weights
        ///     The base strain weights are affected by LN layering
        /// </summary>
        /// <param name="qssData"></param>
        /// <param name="qua"></param>
        /// <param name="assumeHand"></param>
        private void ConvertToStrainHitObjects(float rate, Hand assumeHand)
        {
            // Add hit objects from qua map to qssData
            for (var i = 0; i < Map.HitObjects.Count; i++)
            {
                var curHitOb = new StrainSolverHitObject(Map.HitObjects[i]);
                var curStrainData = new StrainSolverData(curHitOb, rate);

                // Assign Finger and Hand States
                switch (Map.Mode)
                {
                    case GameMode.Keys4:
                        curHitOb.FingerState = LaneToFinger4K[Map.HitObjects[i].Lane];
                        curStrainData.Hand = LaneToHand4K[Map.HitObjects[i].Lane];
                        break;
                    case GameMode.Keys7:
                        curHitOb.FingerState = LaneToFinger7K[Map.HitObjects[i].Lane];
                        curStrainData.Hand = LaneToHand7K[Map.HitObjects[i].Lane] == Hand.Ambiguous ? assumeHand : LaneToHand7K[Map.HitObjects[i].Lane];
                        break;
                }

                // Add Strain Solver Data to list
                StrainSolverData.Add(curStrainData);
            }
        }*/
    }
}
