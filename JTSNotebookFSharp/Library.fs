module JTSNotebookFSharp

open YYZ.JTS
open Plotly.NET
open System

let terrainColorMap = Map [
    "Water", [50; 50; 255]
    "Froest", [10; 255; 10]
    "Orchard", [50; 250; 50]
    "Rough", [100; 100; 50]
]

let defaultTerrainColor = [100; 200; 100]

let roadColors = ["gray"; "yellow"; "red"; "black"]

let rng = Random()

let plotMapFile (terrainMat: HexTerrain[,]) = 
    [for i in 1..terrainMat.GetLength(0) -> 
        [for j in 1..terrainMat.GetLength(1) ->
            match (Map.tryFind terrainMat[i-1, j-1].Name terrainColorMap) with
            | Some(c) -> c
            | None -> defaultTerrainColor
            ]] |> Chart.Image

let plotRoadNetwork (network: HexNetwork) = 
    Seq.zip network.TerrainSystem.Road.Terrains roadColors |>
        Seq.map (fun (roadType, color) -> 
            let c = Color.fromString(color)
            network.SimplifyRoad(roadType) |>
                Seq.map(fun road ->
                    let xl = [for node in road -> node.X]
                    let yl = [for node in road -> node.Y]
                    Chart.Line(x = xl, y = yl, LineColor=c)
                )
        ) |> Seq.concat |> Chart.combine

let computeBoundBox (segments: NetworkSegment seq) = 
    let x, y = 
        segments |> Seq.map (fun seg ->
            seg.Nodes |> Seq.map (fun node -> (node.I, node.J))
        ) |> Seq.concat |> List.ofSeq |> List.unzip
    List.max(x) + 1, List.max(y) + 1

let createEmptyColorMat height width : int array array array =
    [| for i in 1..height -> [| for j in 1..width -> [|  |] |] |]

let plotSegmentMap (segments: NetworkSegment seq) = 
    let maxI, maxJ = computeBoundBox segments
    let colorMat = createEmptyColorMat maxI maxJ

    for seg in segments do
        let color = [| rng.Next(256); rng.Next(256); rng.Next(256) |]
        for node in seg.Nodes do
            colorMat[node.I][node.J] <- color
    Chart.Image(colorMat)

let withMapSize size (width: int, height: int) chart = 
    let f = if height < width then (float)height / size else (float)width / size
    chart |> Chart.withSize((int)((float)width / f), (int)((float)height / f))
          |> Chart.withMarginSize(Left=20, Top=20, Right=20, Bottom=20)

let plotSegmentEdges (segments: NetworkSegment seq) = 
    segments |> Seq.map (fun seg -> 
        seg.Neighbors |> Seq.map (fun nei ->
            let x = [| seg.Center.X; nei.Center.X |]
            let y = [| seg.Center.Y; nei.Center.Y |]
            Chart.Line(x=x, y=y)
        )
    ) |> Seq.concat |> Chart.combine

let plotSegmentCenterTransition (segments: NetworkSegment seq) = 
    segments |> Seq.map (fun seg ->
        let x = [| seg.XMean; (float32)seg.Center.X |]
        let y = [| seg.YMean; (float32)seg.Center.Y |]
        Chart.Line(x = x, y = y, ShowMarkers = true)
    ) |> Chart.combine


let test = 
    let xData = [0. .. 0.1 .. 10.]
    let yData = [for x in xData -> sin(x)]
    Chart.Point(xData, yData)