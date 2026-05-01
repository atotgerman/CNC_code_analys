namespace CncAnalyzer.Web

open WebSharper
open WebSharper.Sitelets
open Microsoft.Data.Sqlite
open System.Text.Json

[<JavaScript false>]
module Server =

    let connectionString = "Data Source=CNCdata.db"

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
    let SaveCncRpc (name: string) (turning: string) (gcode: string) : Async<unit> =
        async {
            saveCnc name turning gcode
    }
