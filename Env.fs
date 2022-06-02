module FScribe.Env

open System
open System.Collections
open System.Collections.Generic
open dotenv.net


let private env =
    lazy
        let dct = Dictionary<string, string>()

        Environment.GetEnvironmentVariables()
        |> Seq.cast<DictionaryEntry>
        |> Seq.iter (fun kv -> dct.Add(string kv.Key, string kv.Value))

        DotEnv.Read()
        |> Seq.iter (fun (KeyValue (k, v)) -> dct.Add(k, v))

        dct

let GetEnv key =
    match env.Value.TryGetValue key with
    | true, value -> value
    | false, _ -> ""
