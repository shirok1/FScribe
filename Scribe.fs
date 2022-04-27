module Scribe

open System
open System.Collections.Generic
open System.IO
open Mirai.Net.Sessions
open Mirai.Net.Data.Messages.Concretes
open Mirai.Net.Data.Messages.Receivers
open Newtonsoft.Json
open Util


type IScribeRecord =
    abstract member MsgId: string
    abstract member Markdown: string


type PlainScribeRecord =
    { MsgId: string
      Markdown: string }
    interface IScribeRecord with
        member x.MsgId = x.MsgId
        member x.Markdown = x.Markdown


type ScribeRecord(msg: GroupMessageReceiver) =
    let senderName = msg.Sender.Name
    let content = msg.MessageChain

    let maybeSource =
        content
        |> Seq.tryHead
        |> Option.bind tryParse<SourceMessage>

    let timestamp =
        maybeSource
        |> Option.map (fun s ->
            DateTimeOffset
                .FromUnixTimeSeconds(
                    int64 s.Time
                )
                .DateTime)
        |> Option.defaultValue DateTime.Now

    let msgId =
        maybeSource
        |> Option.map (fun s -> s.MessageId)
        |> Option.defaultWith timestamp.ToString

    let lazyMarkdown =
        let cst = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")
        let timeChina = TimeZoneInfo.ConvertTimeFromUtc(timestamp, cst)

        lazy
            ($"{senderName} ({timeChina}): "
             + (content
                |> Seq.map (function
                    | :? PlainMessage as plain -> plain.Text
                    | :? AtMessage as atm -> $"@{atm.Target}" // TODO: actual name instead of id
                    | :? FaceMessage as face -> $"[{face.Name}]"
                    | :? FileMessage as file -> $"[文件] {file.Name}"
                    | :? ForwardMessage as forward -> $"[{Seq.length forward.NodeList}条聊天记录]"
                    | :? ImageMessage as img -> $"![{img.ImageId}]({img.Url})"
                    | :? MarketFaceMessage as face -> face.Name
                    | :? MusicShareMessage as music -> $"[{music.Title} - {music.Summary}]({music.MusicUrl})"
                    | :? QuoteMessage as quote -> $"|回复{quote.SenderId}| "
                    | :? SourceMessage -> ""
                    | x -> x.ToString())
                |> String.concat ""))

    member x.Markdown = (x :> IScribeRecord).Markdown

    interface IScribeRecord with
        member _.MsgId = msgId
        member _.Markdown = lazyMarkdown.Value


module Storage =
    let RecordPath = "records.json"

    type RecordStorage = IDictionary<uint, list<IScribeRecord>>
    type PlainRecordStorage = IDictionary<uint, seq<PlainScribeRecord>>

    let mutable private _records: RecordStorage = dict []

    let LoadRecords () =
        logInfo "Loading records from %s." RecordPath

        use file = File.Open(RecordPath, FileMode.OpenOrCreate)
        use reader = new StreamReader(file)

        _records <-
            reader.ReadToEnd()
            |> JsonConvert.DeserializeObject<PlainRecordStorage>
            |> function null -> dict [] | x -> x
            |> dictMap (Seq.cast >> Seq.toList)

        logInfo "Loaded %d records." (_records.Values |> Seq.sumBy List.length)

    let LoadRecordsAsync () = async { LoadRecords() }

    let AllRecords id : seq<IScribeRecord> = _records[uint id]

    let SaveRecords () =
        use file = File.Open(RecordPath, FileMode.OpenOrCreate)
        use writer = new StreamWriter(file)

        writer.Write(_records |> JsonConvert.SerializeObject)

        logInfo "Saved %d records." (_records.Values |> Seq.sumBy List.length)

    let AppendRecord id record =
        let uintId = uint id

        if _records.ContainsKey uintId then
            _records[uintId] <- _records[uintId] @ [ record ]
        else
            _records.Add(uintId, [ record ])


let handle (bot: MiraiBot) (msg: GroupMessageReceiver) =
    msg.MessageChain
    |> Seq.tryFind (function
        | :? AtMessage as atm -> atm.Target = bot.QQ
        | _ -> false) // if at bot
    |> Option.map (fun _ -> msg.MessageChain)
    |> Option.bind (Seq.choose tryParse<QuoteMessage> >> Seq.tryHead) // if quote

    |> function
        | None ->
            let r = ScribeRecord(msg)
            Storage.AppendRecord msg.GroupId r
            logVerbose "Message: %s" r.Markdown

        | Some q -> // at bot in quote
            let all = Storage.AllRecords msg.GroupId

            let skipped =
                all
                |> Seq.skipWhile (fun r -> r.MsgId <> q.MessageId)
                |> Seq.toList

            let selectedRecords = if skipped.IsEmpty then all else skipped

            logInfo "Selected %d records." (selectedRecords |> Seq.length)
            
            let combined =
                selectedRecords
                |> Seq.map (fun r -> r.Markdown)
                |> String.concat "\n"

            logDebug "----Quote-Start----"
            logDebug "%s" combined
            logDebug "----Quote-End----"

            External.CommentCollected combined |> Async.Start
