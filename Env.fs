module Env

open dotenv.net


let private env = lazy DotEnv.Read()

let GetEnv key =
    match env.Value.TryGetValue key with
    | true, value -> value
    | false, _ -> ""
