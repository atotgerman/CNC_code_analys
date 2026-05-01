namespace CncAnalyzer.Web

open WebSharper
open WebSharper.Sitelets
open Microsoft.Data.Sqlite
open System.Text.Json

[<JavaScript false>]
module Server =

    
    
    
    let connectionString = "Data Source=CNCdata.db"

    let initializeDatabase () =
        use conn = new SqliteConnection(connectionString)
        conn.Open()

        use cmd = conn.CreateCommand()

        cmd.CommandText <- """
            CREATE TABLE IF NOT EXISTS cnc_files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT,
                turning TEXT,
                gcode TEXT
            )
        """

        cmd.ExecuteNonQuery() |> ignore

        printfn "Database initialized"

    let saveCnc (name: string) (turning: string) (gcode: string) =
        use conn = new SqliteConnection(connectionString)
        conn.Open()

        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            INSERT INTO cnc_files (name, turning, gcode)
            VALUES (@name, @turning, @gcode)
        """

        cmd.Parameters.AddWithValue("@name", name) |> ignore
        cmd.Parameters.AddWithValue("@turning", turning) |> ignore
        cmd.Parameters.AddWithValue("@gcode", gcode) |> ignore

        cmd.ExecuteNonQuery() |> ignore
    [<Rpc>]
    let SaveCncRpc
        (name:string)
        (turning:string)
        (gcode:string) : Async<unit> =
        async {
                try
                    printfn "RPC CALLED"
                    printfn "name: %s" name
                    printfn "turning: %s" turning

                    saveCnc name turning gcode
                with ex ->
                    printfn "SAVE ERROR: %s" ex.Message
                    printfn "%A" ex
                    raise ex
        }
