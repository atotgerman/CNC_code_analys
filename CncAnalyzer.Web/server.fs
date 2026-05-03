namespace CncAnalyzer.Web

open WebSharper
open WebSharper.Sitelets
open Microsoft.Data.Sqlite
open System.Text.Json

[<JavaScript false>]
module Server =

    
    let generateRandomPath points =
        let rnd = System.Random()

        let lines =
            [|
                yield "Command,X,Y,Comment"
                yield "G0,0,0,Start"

                for i in 1..points do
                    let x = rnd.Next(0,100)
                    let y = rnd.Next(0,100)

                    yield sprintf "G1,%d,%d,Random move" x y
            |]

        String.concat "\n" lines
    
    let connectionString = "Data Source=CNCdata.db"

    let initializeDatabase () =
        use conn = new SqliteConnection(connectionString)
        conn.Open()

        use cmd = conn.CreateCommand()

        let gcode1 = generateRandomPath 10
        let gcode2 = generateRandomPath 20
        let gcode3 = generateRandomPath 30
        let gcode4 = generateRandomPath 30
        let gcode5 = generateRandomPath 35


        cmd.CommandText <- """
            DROP TABLE IF EXISTS cnc_files;

            CREATE TABLE cnc_files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT,
                turning TEXT,
                gcode TEXT
            );
        """
        
        cmd.ExecuteNonQuery() |> ignore
        let insertData name turning gcode =
            use insertCmd = conn.CreateCommand()

            insertCmd.CommandText <- """
                INSERT INTO cnc_files (name, turning, gcode)
                VALUES (@name, @turning, @gcode)
            """

            insertCmd.Parameters.AddWithValue("@name", name) |> ignore
            insertCmd.Parameters.AddWithValue("@turning", turning) |> ignore
            insertCmd.Parameters.AddWithValue("@gcode", gcode) |> ignore

            insertCmd.ExecuteNonQuery() |> ignore

        insertData "Teszt1" "Marás" gcode1
        insertData "Teszt2" "Esztergálás" gcode2
        insertData "Teszt3" "Fúrás" gcode3
        insertData "Teszt4" "Vágás" gcode4
        insertData "Teszt5" "Lyukasztás" gcode5
        
        
        
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
