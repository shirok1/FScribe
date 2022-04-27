open System
open System.Threading
open Mirai.Net.Sessions
open Mirai.Net.Data.Messages.Receivers
open Env
open Util


Scribe.Storage.LoadRecords()


let bot = new MiraiBot()

bot.QQ <- GetEnv "MIRAI_API_QQ"
bot.Address <- GetEnv "MIRAI_API_ADDRESS"
bot.VerifyKey <- GetEnv "MIRAI_API_KEY"

printfn "Connecting to %s." bot.Address
bot.LaunchAsync().Wait()
printfn "Login to %s." bot.QQ


bot.MessageReceived
|> Observable.choose tryParse<GroupMessageReceiver>
|> Observable.subscribe (Scribe.handle bot)
|> ignore

printfn "Scribe is now observing."


let exitEvent = new ManualResetEvent(false)

AppDomain.CurrentDomain.ProcessExit.AddHandler (fun _ _ ->
    Scribe.Storage.SaveRecords()
    exitEvent.Set() |> ignore)

Console.CancelKeyPress.AddHandler (fun _ args ->
    args.Cancel <- true
    exitEvent.Set() |> ignore)

exitEvent.WaitOne() |> ignore

// Scribe.Storage.SaveRecords()

exit 0
