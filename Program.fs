open System
open System.Reactive.Subjects
open System.Threading
open FSharp.Control.Reactive
open Mirai.Net.Sessions
open Mirai.Net.Data.Messages.Receivers
open Serilog
open Serilog.Events
open FScribe
open FScribe.Env
open FScribe.Util


Log.Logger <-
    LoggerConfiguration()
        .MinimumLevel.Is(LogEventLevel.Debug)
        .WriteTo.Console()
        .CreateLogger()

let asmName =
    System
        .Reflection
        .Assembly
        .GetExecutingAssembly()
        .GetName()

logInfo "%s %s" asmName.Name (asmName.Version |> string)

let initialStatus =
    Storage.loadStatus "records.json"

let bot = new MiraiBot()

bot.QQ <- GetEnv "MIRAI_API_QQ"
bot.Address <- GetEnv "MIRAI_API_ADDRESS"
bot.VerifyKey <- GetEnv "MIRAI_API_KEY"

logInfo "Connecting to %s." (bot.Address |> string)
bot.LaunchAsync().Wait()
logInfo "Login to %s." bot.QQ

let latestStatus =
    new BehaviorSubject<Storage.ScribeStatus>(initialStatus)

bot.MessageReceived
|> Observable.choose tryParse<GroupMessageReceiver>
|> Observable.scanInit initialStatus (Scribe.handle bot)
|> Observable.subscribeObserver latestStatus
|> ignore

logInfo "Scribe is now observing."


let saveCurrentStatus () =
    if latestStatus.Value = initialStatus then
        logWarning "No changes to save!"

    Storage.saveStatus "records.json" latestStatus.Value

let exitEvent = new ManualResetEvent(false)

AppDomain.CurrentDomain.UnhandledException.AddHandler (fun _ e ->
    let ex = e.ExceptionObject :?> Exception
    Log.Error(ex, "Unhandled exception.")
    Environment.Exit(System.Runtime.InteropServices.Marshal.GetHRForException(ex)))

AppDomain.CurrentDomain.ProcessExit.AddHandler (fun _ _ ->
    logInfo "ProcessExiting..."
    saveCurrentStatus ()
    exitEvent.Set() |> ignore)

Console.CancelKeyPress.AddHandler (fun _ args ->
    logInfo "Received Ctrl+C. Scribe is now exiting."
    args.Cancel <- true
    saveCurrentStatus ()
    exitEvent.Set() |> ignore)

exitEvent.WaitOne() |> ignore

Log.CloseAndFlush()

exit 0
