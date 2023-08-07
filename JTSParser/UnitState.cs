namespace YYZ.JTS
{

    using System.Collections.Generic;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;

    public abstract class AbstractUnitState
    {
        public AbstractUnit OobItem;
        public int CurrentStrength;
        public Formation Parent = null;
    }



    public class UnitState : AbstractUnitState
    {
        public int Fatigue;
        public int X;
        public int Y;
        public int I{get => Y;}
        public int J{get => X;}
        public float YCorrected{get => X % 2 == 0 ? Y : Y - 0.5f;}

        public int DirectionCode;
        public int Slot;
        public int UsedMovementPoints;
        public int Flags;

        public override string ToString()
        {
            return $"UnitState(X={X}, Y={Y}, CurrentStrength={CurrentStrength}, Fatigue={Fatigue}, DirectionCode={DirectionCode}, Slot={Slot}, UsedMovement={UsedMovementPoints}, Flags={Flags}, {OobItem})";
        }

        public UnitDirection Direction { get => (UnitDirection)DirectionCode; }

        public float Distance2(UnitState other)
        {
            var dx = other.X - X;
            var dy = other.Y - Y;
            return dx*dx + dy*dy;
        }

        public float Distance2Corrected(UnitState other)
        {
            var dx = other.X - X;
            var dy = other.YCorrected - YCorrected;
            return dx*dx + dy*dy;
        }

        public float Distance(UnitState other) => MathF.Sqrt(Distance2(other));
        public float DistanceCorrected(UnitState other) => MathF.Sqrt(Distance2Corrected(other));
        
        // public float Distance()
    }

    public class Formation : AbstractUnitState // Brigade
    {
        public int XAnchored;
        public int YAnchored;
        public float XMean; // weighted by strength, fallback to XMeanUnit if strength is 0
        public float YMean;
        public float XMeanUnit; // average directly on unit position
        public float YMeanUnit;
        public int IAnchored{get => YAnchored;}
        public int JAnchored{get => XAnchored;}
        public UnitGroup Group{get => (UnitGroup)OobItem;}
        public List<UnitState> States = new();
        public List<Formation> SubFormations = new();
        public List<AbstractUnitState> AbstractStates = new();
        public HashSet<HexPosition> HexPositions = new();
        public List<UnitState> FlattenStates = new();

        public override string ToString()
        {
            var parentStr = Parent == null ? "" : $" ,{Parent.Group}";
            return $"Formation({Group}, CurrentStrength:{CurrentStrength}, States:[{States.Count}], SubFormations:[{SubFormations.Count}], AbstractStates:[{AbstractStates.Count}], HexPositions:[{HexPositions.Count}]{parentStr})";
        }

        public void Add(UnitState state)
        {
            States.Add(state);
            AbstractStates.Add(state);
            FlattenStates.Add(state);
        }

        public void Add(Formation formation)
        {
            SubFormations.Add(formation);
            AbstractStates.Add(formation);
        }

        public void CalculateFrozenValue()
        {
            CurrentStrength = 0;
            HexPositions.Clear();

            var x = 0f;
            var y = 0f;
            var xW1 = 0f;
            var yW1 = 0f;

            foreach(var abstractState in AbstractStates)
            {
                abstractState.Parent = this;

                var unitState = abstractState as UnitState;
                if(unitState != null)
                {
                    CurrentStrength += unitState.CurrentStrength;
                    x += unitState.CurrentStrength * unitState.X;
                    y += unitState.CurrentStrength * unitState.Y;
                    xW1 += unitState.X;
                    yW1 += unitState.Y;
                    HexPositions.Add(new HexPosition(){X=unitState.X, Y=unitState.Y});
                }
                var formation = abstractState as Formation;
                if(formation != null)
                {
                    formation.CalculateFrozenValue();

                    CurrentStrength += formation.CurrentStrength;
                    x += formation.XMean * formation.CurrentStrength;
                    y += formation.YMean * formation.CurrentStrength;
                    foreach(var hexPosition in formation.HexPositions)
                    {
                        HexPositions.Add(hexPosition);
                        xW1 += hexPosition.X;
                        yW1 += hexPosition.Y;
                    }

                    FlattenStates.AddRange(formation.FlattenStates);
                }
            }

            XMeanUnit = xW1 / HexPositions.Count;
            YMeanUnit = yW1 / HexPositions.Count;

            if(CurrentStrength > 0)
            {
                XMean = x / CurrentStrength;
                YMean = y / CurrentStrength;
            }
            else // fallback for strange situation in some scenario
            {
                XMean = XMeanUnit;
                YMean = YMeanUnit;
            }


            var minPosition = Utils.MinBy(HexPositions, p => Utils.Distance2(p.X, p.Y, XMean, YMean));

            XAnchored = minPosition.X;
            YAnchored = minPosition.Y;
        }
    }

    public class JTSUnitStates
    {
        // Extract unit state (position, direction, current strength, disorder & fatigue state)
        // from a scenario file (*.scn) or a save file (*.btl, *.bte)
        public override string ToString()
        {
            return $"JTSUnitStates({UnitStates.Count})";
        }

        public List<UnitState> UnitStates = new();
        public Formation FormationRoot;
        public Dictionary<UnitGroup, Formation> Group2Formation = new();

        public HashSet<string> PresentCountries()
        {
            return FormationRoot.Group.Units.Select(oob => oob.Country).ToHashSet();
        }

        // public Dictionary<AbstractUnit, UnitState> Unit2state = new(); // OOB unit can be divided into multiple map units so it's not well defined 

        // 1 2.3.4.4 4 4 393 300 0 4194304 36 23
        // [0]: 1: Dynamic Command Type (Unit Info, reinforcement, ...)
        // [1]: 2.3.4.4: Locate a unit in the OOB file
        // [2]: 4: Direction. 1 => Top-Right, 2=> Right, 4 => Down-Right, 8 => Down-Right, 16 => Left, 32 => Top-Left
        // [3]: 4: Some Sort of "Slot", non-leader units in the same hex will has a unique enum value (1, 2, 4, 8, ...). However I can't see how does it effect gameplay and it's also not the order shown in game.
        // [4]: 393: Current Strength
        // [5]: 300: Fatigue
        // [6]: 0: Used Movement
        // [7]: 4194304: A lot of binary Flags, which encode formation, disorder, formation, isolate and etc... 
        // [8]: X
        // [9]: Y
        static string unitPattern = @"(\d+) ((?:\d+\.)*\d+) (\d+) (\d+) (\d+) (\d+) (\d+) (\d+) (\d+) (\d+)";

        [Obsolete("Extract is deprecated, please use ExtractByLines instead.")]
        public void Extract(UnitGroup oobRoot, string s) // 
        {
            foreach(Match match in Regex.Matches(s, unitPattern)) // Groups[0] => full match, Groups[1] => First group, ...
            {
                var oobIndex = match.Groups[2].Value;

                var unitSelected = oobRoot.Select(oobIndex);

                var unitState = new UnitState()
                {
                    OobItem = unitSelected,
                    CurrentStrength = int.Parse(match.Groups[5].Value),
                    Fatigue = int.Parse(match.Groups[6].Value),
                    X = int.Parse(match.Groups[9].Value),
                    Y = int.Parse(match.Groups[10].Value),
                    DirectionCode = int.Parse(match.Groups[3].Value),
                    Slot = int.Parse(match.Groups[4].Value),
                    UsedMovementPoints = int.Parse(match.Groups[7].Value),
                    Flags = int.Parse(match.Groups[8].Value)
                };

                UnitStates.Add(unitState);
            }
        }

        public void ExtractByLines(UnitGroup oobRoot, IEnumerable<string> sl)
        {
            CreateUnitStates(oobRoot, sl);
            FormationRoot = CreateFormationsAsRoot();
            FormationRoot.CalculateFrozenValue();
            // AssignParentStrengthXYList();
            //  Assign Parent and Strength
        }

        protected void CreateUnitStates(UnitGroup oobRoot, IEnumerable<string> sl)
        {
            foreach (var s in sl)
            {
                if (s[0] == ' ')
                    continue;

                // var ss = s.Trim().Split(" ");
                var ss = s.Trim().Split();

                if (ss[0] != "1")
                    continue;
                
                var oobIndex = ss[1];
                var unitSelected = oobRoot.Select(oobIndex);

                var unitState = new UnitState()
                {
                    OobItem = unitSelected,
                    CurrentStrength = int.Parse(ss[4]),
                    Fatigue = int.Parse(ss[5]),
                    X = int.Parse(ss[8]),
                    Y = int.Parse(ss[9]),
                    DirectionCode = int.Parse(ss[2]),
                    Slot = int.Parse(ss[3]),
                    UsedMovementPoints = int.Parse(ss[6]),
                    Flags = int.Parse(ss[7])
                };

                UnitStates.Add(unitState);
                // Unit2state[unitSelected] = unitState;
            }
        }

        public Dictionary<string, List<UnitState>> GroupByCountry()
        {
            var ret = new Dictionary<string, List<UnitState>>();
            foreach(var unitState in UnitStates)
            {
                var country = unitState.OobItem.Country;
                if (ret.TryGetValue(country, out var unitStateList))
                {
                    unitStateList.Add(unitState);
                }
                else
                {
                    ret[country] = new List<UnitState>() { unitState };
                }
            }
            return ret;
        }

        public Dictionary<UnitGroup, List<UnitState>> GroupByBrigade()
        {
            var ret = new Dictionary<UnitGroup, List<UnitState>>();
            foreach(var unitState in UnitStates)
            {
                var parent = unitState.OobItem.Parent;
                var group = parent as UnitGroup;
                if(group != null && group.Size == "B")
                {
                    if(ret.TryGetValue(group, out var unitStateList))
                    {
                        unitStateList.Add(unitState);
                    }
                    else
                    {
                        ret[group] = new List<UnitState>() { unitState };
                    }
                }
            }
            return ret;
        }

        public List<Formation> GetBrigadeFormations()
        {
            var formations = new List<Formation>();
            foreach((var unitGroup, var formation) in Group2Formation)
            {
                if(unitGroup.Size == "B")
                    formations.Add(formation);
            }
            return formations;
        }

        protected Formation CreateFormationsAsRoot()
        {
            // Group2Formation = new Dictionary<UnitGroup, Formation>();
            // var openFormations = new List<Formation>();
            foreach(var unitState in UnitStates)
            {
                var parent = unitState.OobItem.Parent;
                if(Group2Formation.TryGetValue(parent, out var formation))
                {
                    // formation.States.Add(unitState);
                    formation.Add(unitState);
                }
                else
                {
                    formation = Group2Formation[parent] = new Formation(){OobItem=parent};
                    formation.Add(unitState);
                }
            }

            var openFormations = Group2Formation.Values.ToList();
            Formation rootFormation = null;
            while(openFormations.Count > 0)
            {
                var newOpenFormations = new List<Formation>();
                foreach(var subFormation in openFormations)
                {
                    var parent = subFormation.Group.Parent;
                    if(parent == null)
                    {
                        rootFormation = subFormation;
                        continue;
                    }
                    if(Group2Formation.TryGetValue(parent, out var formation))
                    {
                        formation.Add(subFormation);
                    }
                    else
                    {
                        formation = Group2Formation[parent] = new Formation(){OobItem=parent};
                        formation.Add(subFormation);
                        newOpenFormations.Add(formation);
                    }
                }
                openFormations = newOpenFormations;
            }
            return rootFormation;
        }
    }
}