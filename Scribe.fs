module FScribe.Scribe

open System
open FSharpPlus
open FSharpPlus.Data
open Microsoft.FSharp.Collections
open Mirai.Net.Sessions
open Mirai.Net.Data.Messages.Concretes
open Mirai.Net.Data.Messages.Receivers
open FScribe.Msg
open FScribe.Util


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
        let cst =
            TimeZoneInfo.FindSystemTimeZoneById("China Standard Time")

        let timeChina =
            TimeZoneInfo.ConvertTimeFromUtc(timestamp, cst)

        lazy
            ($"{senderName} ({timeChina}): "
             + (content
                |> Seq.map (function
                    | :? PlainMessage as plain -> plain.Text
                    | :? AtMessage as atm -> $"@{atm.Target}" // TODO: actual name instead of id
                    | :? AppMessage as app -> $"{app.Content}"
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

    member x.Markdown =
        (x :> IScribeRecord).Markdown

    interface IScribeRecord with
        member _.MsgId = msgId
        member _.Markdown = lazyMarkdown.Value


type MessageCollectContext =
    { TimeLimit: DateTime
      TargetMsgId: string }

type GroupContext =
    { Records: IScribeRecord list
      Collecting: MessageCollectContext option }
    static member Default: GroupContext =
        { Records = List.empty
          Collecting = None }

let handle (bot: MiraiBot) (someMap: Map<uint, GroupContext>) (msg: GroupMessageReceiver) =
    //    logInfo "Handling message."
    let groupId = uint msg.GroupId

    let ctx =
        someMap
        |> Map.tryFind groupId
        |> Option.defaultValue GroupContext.Default

    let isAtBot =
        msg.MessageChain
        |> Seq.exists (function
            | :? AtMessage as atm -> atm.Target = bot.QQ
            | _ -> false)

    let maybeQuote =
        msg.MessageChain
        |> Seq.choose tryParse<QuoteMessage>
        |> Seq.tryHead

    //    match ctx.Collecting with
//    | Some { TimeLimit = time; TargetMsgId = id } when time >= DateTime.Now ->
//        // TODO: check for caller
//        msg.Sender.Group
//        |> MrGroup.send "五秒内回了"
//        |> Async.Start
//
//        someMap
//        |> Map.add groupId { ctx with Collecting = None }
//    | Some _ ->
//        // TODO: check for caller
//        msg.Sender.Group
//        |> MrGroup.send "没能五秒内回"
//        |> Async.Start
//
//        someMap
//        |> Map.add groupId { ctx with Collecting = None }
//    | _ when isAtBot ->
//        // TODO: me is at?
//        logInfo "At bot."
//
//        msg.Sender.Group
//        |> MrGroup.send "试试五秒内回复我"
//        |> Async.Start
//
//        someMap
//        |> Map.add
//            groupId
//            { ctx with
//                Collecting =
//                    Some
//                        { TimeLimit = DateTime.Now + TimeSpan.FromSeconds(5)
//                          TargetMsgId = "" } }
//    | _ ->
//        // Just record it
//        let r = ScribeRecord(msg)
//        logDebug "Message: %s" r.Markdown
//
//        someMap
//        |> Map.add groupId { ctx with Records = r :: ctx.Records }

    match isAtBot, maybeQuote with
    | true, None
    | false, _ ->
        let r = ScribeRecord(msg)
        logVerbose "Message: %s" r.Markdown

        someMap
        |> Map.add groupId { ctx with Records = r :: ctx.Records }

    | true, Some q -> // at bot in quote
        let skipped =
            ctx.Records
            |> List.skipWhile (fun r -> r.MsgId <> q.MessageId)

        let selectedRecords =
            if skipped.IsEmpty then
                ctx.Records
            else
                skipped

        logInfo "Selected %d records." (selectedRecords |> Seq.length)

        let combined =
            selectedRecords |> List.map (fun r -> r.Markdown)

        logDebug "----Quote-Start----"
        logDebug "%s" (string combined)
        logDebug "----Quote-End----"

        External.commentCollected combined |> Async.Start

        someMap
