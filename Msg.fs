module FScribe.Msg

open Mirai.Net.Data.Shared
open Mirai.Net.Sessions.Http.Managers


module MrGroup =
    let sendAnd (text: string) (group: Group) =
        MessageManager.SendGroupMessageAsync(group, text)
        |> Async.AwaitTask

    let send it group = sendAnd it group |> Async.Ignore
