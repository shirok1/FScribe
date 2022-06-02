module FScribe.Storage

open System.IO
open Util
open Scribe
open Newtonsoft.Json

type ScribeStatus = Map<uint, GroupContext>

let loadStatus path : ScribeStatus =
    logInfo "Loading records from %s." path

    use file =
        File.Open(path, FileMode.OpenOrCreate)

    use reader = new StreamReader(file)

    let wholeRecords =
        reader.ReadToEnd()
        |> JsonConvert.DeserializeObject<Map<uint, PlainScribeRecord list>>
        |> Map.map (fun _ records ->
            { GroupContext.Default with Records = records |> List.map (fun t -> t :> IScribeRecord) })

    logInfo
        "Loaded %d records."
        (wholeRecords.Values
         |> Seq.sumBy (fun group -> List.length group.Records))

    wholeRecords


let saveStatus path (records: ScribeStatus) =
    use file = File.Open(path, FileMode.Create)
    use writer = new StreamWriter(file)

    records
    |> Map.map (fun _ ctx -> ctx.Records)
    |> JsonConvert.SerializeObject
    |> writer.Write

    logInfo
        "Saved %d records."
        (records.Values
         |> Seq.sumBy (fun group -> List.length group.Records))
