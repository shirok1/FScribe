module Util

open System.Collections.Generic


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
