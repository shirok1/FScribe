module FScribe.Util

open System.Collections.Generic
open Serilog


let tryParse<'t> (input: obj) : 't option =
    if input :? 't then
        Some(input :?> 't)
    else
        None

let dictMap f (x: IDictionary<'Key, 'T>) =
    let dct = Dictionary<'Key, 'U>()

    for KeyValue (k, v) in x do
        dct.Add(k, f v)

    dct :> IDictionary<'Key, 'U>

let logVerbose fmt = Printf.kprintf Log.Verbose fmt
let logDebug fmt = Printf.kprintf Log.Debug fmt
let logInfo fmt = Printf.kprintf Log.Information fmt
let logWarning fmt = Printf.kprintf Log.Warning fmt
let logError fmt = Printf.kprintf Log.Error fmt
let logFatal fmt = Printf.kprintf Log.Fatal fmt
