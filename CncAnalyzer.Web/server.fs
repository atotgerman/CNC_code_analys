namespace CncAnalyzer.Web

open WebSharper
open WebSharper.Sitelets
open System.Data.SQLite

[<JavaScript false>]
module Server =

    let connectionString = "Data Source=CNCdata.db"

    let saveCnc (name: string) (turning: string) (imagePath: string) =
        use conn = new SQLiteConnection(connectionString)
        conn.Open()

        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            INSERT INTO cnc_files (name, turning, image_path)
            VALUES (@name, @turning, @image)
        """

        cmd.Parameters.AddWithValue("@name", name) |> ignore
        cmd.Parameters.AddWithValue("@turning", author) |> ignore
        cmd.Parameters.AddWithValue("@image", imagePath) |> ignore

        cmd.ExecuteNonQuery() |> ignore