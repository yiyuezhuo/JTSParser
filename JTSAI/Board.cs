using YYZ.JTS;
using System;
using YYZ.PathFinding;
using YYZ.PathFinding2;
using System.Collections.Generic;
using System.Linq;
using YYZ.AI.PointToPointAllocator;


namespace YYZ.JTS.AI
{
    /*
    public class GenericPosition<T>: IPosition
    {
        public T Origin;

        public override bool Equals(object obj)
        {
            return obj is GenericPosition<T> position &&
                   EqualityComparer<T>.Default.Equals(Origin, position.Origin);
        }

        public override int GetHashCode()
        {
            return Origin.GetHashCode();
        }
    }

    public class HexPosition: GenericPosition<Hex>
    {

    }
    */

    
    public class HexPosition: IPosition
    {
        public Hex Origin;

        public override bool Equals(object obj)
        {
            return obj is HexPosition position &&
                   EqualityComparer<Hex>.Default.Equals(Origin, position.Origin);
        }

        public override int GetHashCode()
        {
            return Origin.GetHashCode();
        }
    }
    

    public class HexUnit: IUnit
    {
        public UnitState Origin;
        public float CombatValue{get => Origin.CurrentStrength;} // TODO: handle gun
        public IPosition Position{get; set;}
    }

    
    public abstract class GenericBoard<TPosition, THex>: IBoard where TPosition: IPosition
    {
        public float AllowancePerTurn = 10;
        public List<float> MoveTime(IPosition src, IEnumerable<IPosition> positions)
        {
            // I dont't want use complicated generic to write experimenting code so I just downcast,
            // so a little performance hit and introduce some obscure implicity constraints which could be enforced by generic.
            // For example, positions should be the same concrete class, which can be enforced by generic, but I rather not to write many 
            // generic type shits.
            var _src = GetHex((TPosition)src);
            var _positions = positions.Select(p => GetHex((TPosition)p)).ToList();

            var res = GetPathFinder().GetShortpathForMultiple(_src, _positions);
            var ret = new List<float>();
            foreach(var pos in _positions)
                if(res.TryGetValue(pos, out var path))
                    ret.Add(path.cost / AllowancePerTurn);
                else
                    throw new ArgumentException("AI for Disconnected is not implemented");
            return ret;
        }
        public abstract IPathFinder<THex> GetPathFinder();
        public abstract THex GetHex(TPosition pos);
        public abstract IEnumerable<IUnit> ActivesUnits{get;}
        public abstract IEnumerable<IUnit> PassiveUnits{get;}
    }
    

    public class HexBoard: GenericBoard<HexPosition, Hex>
    {
        public GameState S;
        public GraphWrapper G;
        public HashSet<string> activeCountries;

        IEnumerable<IUnit> SelectUnits(bool selectActive)
        {
            foreach(var state in S.UnitStates.UnitStates)
            {
                var isActive = activeCountries.Contains(state.OobItem.Country);
                if((isActive == selectActive) && (!isActive == !selectActive))
                {
                    var hex = S.Network.HexMat[state.I, state.J];
                    var position = new HexPosition(){Origin=hex};
                    var unit = new HexUnit()
                    {
                        Origin=state,
                        Position=position
                    };
                    yield return unit;
                }
            }
        }

        public override Hex GetHex(HexPosition pos) => pos.Origin;
        public override IPathFinder<Hex> GetPathFinder() => G.StaticPathFinder;
        public override IEnumerable<IUnit> ActivesUnits
        {
            get => SelectUnits(true);
        }
        public override IEnumerable<IUnit> PassiveUnits
        {
            get => SelectUnits(false);
        }
    }

    public class SegmentPosition: IPosition
    {
        public NetworkSegment Origin;

        public override bool Equals(object obj)
        {
            return obj is SegmentPosition position &&
                   EqualityComparer<NetworkSegment>.Default.Equals(Origin, position.Origin);
        }

        public override int GetHashCode()
        {
            return Origin.GetHashCode();
        }
    }

    public class SegmentUnit: IUnit // Unit, brigade or divison
    {
        public Formation Origin;
        public float CombatValue{get; set;} // TODO: handle gun
        public IPosition Position{get; set;}
    }

    
    public class SegmentBoard: GenericBoard<SegmentPosition, NetworkSegment>
    {
        // public GameState S;
        // public SegmentGraph SegmentGraph;
        IPathFinder<NetworkSegment> pathFinder;
        List<SegmentUnit> activesUnits;
        List<SegmentUnit> passiveUnits;

        public static SegmentBoard Generate(GameState state, SegmentGraph segmentGraph, HashSet<string> activeSet)
        {
            var brigades = state.UnitStates.GetBrigadeFormations();

            var actives = new List<SegmentUnit>();
            var passives = new List<SegmentUnit>();

            foreach(var brigade in brigades)
            {
                var hexAnchored = state.Network.HexMat[brigade.IAnchored, brigade.JAnchored];
                var segmentAnchored = segmentGraph.SegmentMap[hexAnchored];
                var strength = brigade.States.Sum(s => s.CurrentStrength);
                var positionStub = new SegmentPosition()
                {
                    Origin=segmentAnchored
                };
                var unit = new SegmentUnit()
                {
                    Origin=brigade,
                    CombatValue=strength,
                    Position=positionStub
                };

                var list = activeSet.Contains(brigade.OobItem.Country) ? actives : passives;
                list.Add(unit);
            }

            var pathFinder = FrozenGraph2D<NetworkSegment>.GetPathFinder(segmentGraph, seg => (seg.XMean, seg.YMean));

            return new()
            {
                pathFinder=pathFinder,
                activesUnits=actives,
                passiveUnits=passives
            };
        }

        public override NetworkSegment GetHex(SegmentPosition pos) => pos.Origin;
        public override IPathFinder<NetworkSegment> GetPathFinder() => pathFinder;
        public override IEnumerable<IUnit> ActivesUnits{get=>activesUnits;}
        public override IEnumerable<IUnit> PassiveUnits{get=>passiveUnits;}
    }
    
}