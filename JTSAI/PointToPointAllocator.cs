
using System;
using System.Linq;
using System.Collections.Generic;

namespace YYZ.AI.PointToPointAllocator
{
    public interface IBoard
    {
        public IEnumerable<IUnit> ActivesUnits{get;}
        public IEnumerable<IUnit> PassiveUnits{get;}
        // public IEnumerable<IUnit> UnitsOf(IPosition position);
        public List<float> MoveTime(IPosition src, IEnumerable<object> positions); // This use normal graph or blocked graph
    }

    public interface IPosition
    {
    }

    public interface IUnit
    {
        public float CombatValue{get;}
        public IPosition Position{get;}
    }



    public class SimplePointToPointAllocator
    {
        public float Coef = 1f;
        public float DiscountRate = 0.1f;
        public float LanchesterPercent = 0.5f;
        public float DiversionaryTimeLambda = 5f;
        public float DiversionaryPercentLambda = 1f;
        public float DiversionaryCoef = 1f;
        public float LossCoef = 5f;

        class BoardWrapper
        {
            public UnitWrapper[] ActiveUnits;
            public UnitWrapper[] PassiveUnits;
            public PositionWrapper[] Sources;
            public PositionWrapper[] Targets;
            public Dictionary<IUnit, UnitWrapper> UnitMap;
            public Dictionary<IPosition, PositionWrapper> PositionMap;

            public static BoardWrapper Create(IBoard board)
            {
                var units = new List<IUnit>(board.ActivesUnits);
                units.AddRange(board.PassiveUnits);

                var unitMap = units.ToDictionary(u => u, u => new UnitWrapper(){Origin=u});
                var positionMap = units.Select(u => u.Position).Distinct()
                    .ToDictionary(p => p, p => new PositionWrapper(){Origin=p});

                foreach((var unit, var unitWrapper) in unitMap)
                {
                    var position = unit.Position;
                    var positionWrapper = positionMap[position];
                    unitWrapper.Position = positionWrapper;
                    positionWrapper.Units.Add(unitWrapper);
                }

                var activeUnits = board.ActivesUnits.Select(u => unitMap[u]).ToArray();
                var passiveUnits = board.PassiveUnits.Select(u => unitMap[u]).ToArray();
                var sources = activeUnits.Select(u => u.Position).Distinct().ToArray();
                var targets = passiveUnits.Select(u => u.Position).Distinct().ToArray();

                foreach(var source in sources)
                {
                    var times = board.MoveTime(source.Origin, targets.Select(t => t.Origin));
                    for(var i=0; i<targets.Length; i++)
                        source.MoveTimeMap[targets[i]] = times[i];
                }

                foreach(var unit in passiveUnits)
                    unit.Position.PassiveTotalCombatValue += unit.CombatValue;

                return new()
                {
                    ActiveUnits=activeUnits,
                    PassiveUnits=passiveUnits,
                    Sources=sources,
                    Targets=targets,
                    UnitMap=unitMap,
                    PositionMap=positionMap
                };
            }
        }

        class UnitWrapper
        {
            public IUnit Origin;
            public float CombatValue{get => Origin.CombatValue;}
            public PositionWrapper Position;
            // Attached variables
            public PositionWrapper AssignedPosition;
            public void AssignTo(PositionWrapper position)
            {
                if(position != null)
                    position.AssginedUnits.Remove(this);
                Position = position;
                position.AssginedUnits.Add(this);
            }
        }

        class PositionWrapper
        {
            public IPosition Origin;
            public Dictionary<PositionWrapper, float> MoveTimeMap = new();
            public List<UnitWrapper> Units = new();
            public float PassiveTotalCombatValue = 0f;
            public float AssignedTotalCombatValue() => AssginedUnits.Sum(u => u.CombatValue);
            // Attached variables
            public HashSet<UnitWrapper> AssginedUnits = new();
        }

        class Assignment
        {
            public float ArrivalTime;
            public UnitWrapper Unit;
            public PositionWrapper Position;
        }


        public void Allocate(IBoard _board)
        {
            var board = BoardWrapper.Create(_board);

            var assignments = new List<Assignment>();

            foreach(var unit in board.ActiveUnits)
                foreach(var target in board.Targets)
                {
                    assignments.Add(new()
                    {
                        ArrivalTime=unit.Position.MoveTimeMap[target],
                        Unit=unit,
                        Position=target
                    });
                }
            
            assignments.Sort((x,y) => x.ArrivalTime.CompareTo(y.ArrivalTime));

            foreach(var assignment in assignments)
            {
                if(assignment.Unit.AssignedPosition == null)
                {
                    assignment.Unit.AssignTo(assignment.Position);
                }
                else
                {
                    TryUpdate(assignment.Unit, assignment.Position);
                }
            }

            while(true)
            {
                var updateAny = false;

                foreach(var active in board.ActiveUnits)
                {
                    foreach(var target in board.Targets)
                    {
                        updateAny |= TryUpdate(active, target);
                    }
                }

                if(!updateAny)
                    break;
            }

        }

        bool TryUpdate(UnitWrapper unit, PositionWrapper dst)
        {
            var src = unit.AssignedPosition;

            var oldValue = PositionValue(src) + PositionValue(dst);

            unit.AssignTo(dst);

            var newValue = PositionValue(src) + PositionValue(dst);

            if(oldValue > newValue)
            {
                unit.AssignTo(src);
                return false;
            }
            return true;
        }

        float PositionValue(PositionWrapper position)
        {
            var pairs = position.AssginedUnits.Select(u => (u, u.Position.MoveTimeMap[position])).ToList();
            pairs.Sort((x,y) => x.Item2.CompareTo(y.Item2)); // Use SortedSet or SortedList?

            var totalCombatValue = 0f;
            var presentValues = new List<float>();

            var massPInt = 0f;
            var lastT = -1f;
            float massP;
            foreach((var unit, var t) in pairs)
            {
                if(lastT != -1f)
                {
                    massP = DiversionaryTime(t) - DiversionaryTime(lastT);
                    massPInt += massP * DiversionaryPercent(totalCombatValue, position.PassiveTotalCombatValue);
                }
                lastT = t;

                totalCombatValue += unit.CombatValue;

                var model = new LanchesterQuadroticSolution(){Red0=totalCombatValue, Blue0=position.PassiveTotalCombatValue};
                model.FightToMinPercent(LanchesterPercent);
                var value = model.BlueLoss - model.RedLoss;

                var discountFactor = 1 / MathF.Pow(1 + DiscountRate, t);
                var presentValue = value * discountFactor;

                presentValues.Add(presentValue);
            }

            if(lastT == -1f)
                throw new ArgumentException("position should have at least 1 element");

            massP = 1f - DiversionaryTime(lastT);
            massPInt += massP * DiversionaryPercent(totalCombatValue, position.PassiveTotalCombatValue);

            return LossCoef * MathF.Max(0, presentValues.Max()) + DiversionaryCoef * massPInt;
        }

        // CDF of Exponential distribution
        static float ExpCdf(float x, float lam) => 1f - MathF.Exp(-x * lam);
        float DiversionaryPercent(float assignedTotalCombatValue, float passiveTotalCombatValue) => ExpCdf(assignedTotalCombatValue / passiveTotalCombatValue, DiversionaryPercentLambda);
        float DiversionaryTime(float t) => ExpCdf(t, DiversionaryTimeLambda);
    }
}