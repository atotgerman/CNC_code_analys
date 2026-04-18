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
        let parts = line.Split([|' '|], System.StringSplitOptions.RemoveEmptyEntries)

        let tryGet (prefix:string) =
            parts
            |>Array.tryFind (fun (p: string) -> p.StartsWith(prefix))
            |> Option.map (fun p -> p.Substring(1) |> float)

        let cmd =
            parts
            |> Array.tryFind (fun p -> p.StartsWith("G"))
            |> Option.defaultValue ""

        {
                Cmd = cmd
                X = tryGet "X"
                Y = tryGet "Y"
        }
    let parseGCode (text: string) =
            text.Split('\n')
            |> Array.map (fun l -> l.Trim())
            |> Array.filter (fun l -> l <> "")
            |> Array.map parseLine
