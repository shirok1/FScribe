open System
open System.Threading
open FSharp.Control.Reactive
open Mirai.Net.Sessions
open Mirai.Net.Data.Messages.Receivers
open Env
open Serilog
open Serilog.Events
open Util


Log.Logger <-
    LoggerConfiguration()
        .MinimumLevel.Is(LogEventLevel.Verbose)
        .WriteTo.Console()
        .CreateLogger()


Scribe.Storage.LoadRecords()


let bot = new MiraiBot()

bot.QQ <- GetEnv "MIRAI_API_QQ"
bot.Address <- GetEnv "MIRAI_API_ADDRESS"
bot.VerifyKey <- GetEnv "MIRAI_API_KEY"

logInfo "Connecting to %s." (bot.Address |> string)
bot.LaunchAsync().Wait()
logInfo "Login to %s." bot.QQ


bot.MessageReceived
|> Observable.choose tryParse<GroupMessageReceiver>
|> Observable.fold (Scribe.handle bot) None
|> ignore

logInfo "Scribe is now observing."


let exitEvent = new ManualResetEvent(false)

AppDomain.CurrentDomain.ProcessExit.AddHandler (fun _ _ ->
    logInfo "ProcessExiting..."
    Scribe.Storage.SaveRecords()
    exitEvent.Set() |> ignore)

Console.CancelKeyPress.AddHandler (fun _ args ->
    logInfo "Received Ctrl+C. Scribe is now exiting."
    args.Cancel <- true
    exitEvent.Set() |> ignore)

exitEvent.WaitOne() |> ignore

// Scribe.Storage.SaveRecords()

Log.CloseAndFlush()

exit 0
