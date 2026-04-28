namespace CncAnalyzer.Web

open WebSharper
open WebSharper.Sitelets
open Microsoft.Data.Sqlite

[<JavaScript false>]
module Server =

    let connectionString = "Data Source=CNCdata.db"

    let saveCnc (name: string) (turning: string) (imagePath: string) =
        use conn = new SqliteConnection(connectionString)
        conn.Open()

        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            INSERT INTO cnc_files (name, turning, image_path)
            VALUES (@name, @turning, @image)
        """

        cmd.Parameters.AddWithValue("@name", name) |> ignore
        cmd.Parameters.AddWithValue("@turning", turning) |> ignore
        cmd.Parameters.AddWithValue("@image", imagePath) |> ignore

        cmd.ExecuteNonQuery() |> ignore
    [<Rpc>]
    let SaveCncRpc (name: string) (turning: string) (imagePath: string) : Async<unit> =
        async {
            saveCnc name turning imagePath
    }
