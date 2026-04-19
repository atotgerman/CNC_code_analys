namespace CncAnalyzer.Web

open WebSharper

[<JavaScript>]
module Parser =

    type GCodeLine = {
        Cmd: string
        X: float option
        Y: float option
    }

    let parseLine (line: string) =
        let parts = line.Split(',')

        if parts.Length >= 3 then
            let cmd = parts.[0].Trim()

            let tryParseFloat (s: string) =
                match System.Double.TryParse(s) with
                | true, v -> Some v
                | _ -> None

            let x = tryParseFloat parts.[1]
            let y = tryParseFloat parts.[2]

            {
                Cmd = cmd
                X = x
                Y = y
            }
        else
            {
                Cmd = ""
                X = None
                Y = None
            }
    let parseGCode (text: string) =
            text.Split('\n')
            |> Array.map (fun l -> l.Trim())
            |> Array.filter (fun l -> l <> "")
            |> Array.skip 1 
            |> Array.map parseLine
