namespace YYZ.JTS.NB
{
    /*
     *  32---- 1
     * 16/    \2 
     *   \____/
     *  8     4
     */
    public enum UnitDirection
    {
        RightTop = 1,
        Right = 2,
        RightBottom = 4,
        LeftBottom = 8,
        Left = 16,
        LeftTop = 32
    }

    public enum TerrainType
    {
        Water,
        Rough,
        Marsh,
        Village,
        Building,
        Chateau,
        Orchard,
        Clear,
        Field,
        Forest,
        Blocked,
        // Civil War Battles
        Town, // = Village?
        // Panzer Campaign
        City
    }

    public enum RoadType
    {
        Path, // Small road, (In CWB, it's called "Trail")
        Road,
        Pike, // Major road
        Railway
    }

    public enum RiverType
    {
        Stream,
        Creek
    }

    public enum HexDirection
    {
        Top,
        TopRight,
        BottomRight,
        Bottom,
        BottomLeft,
        TopLeft
    }

    /*
    public enum GroupSize
    {
        Army,
        Wing,
        Corp,
        Division,
        Brigade,
    }
    */

    public enum UnitCategory
    {
        Infantry,
        Cavalry,
        Artillery
    }

    public enum JTSSeries
    {
        NapoleonicBattle, // NB
        CivilWarBattle, // CWB
        PanzerCampaign, // PZC
        SquadBattle // SB
    }

}