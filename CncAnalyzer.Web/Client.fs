namespace CncAnalyzer.Web

open WebSharper
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.UI.Html
open WebSharper.JavaScript

[<JavaScript>]
module Client =

    type IndexTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>
    let fileContent = Var.Create ""
    type Page =
        | Home
        | Analyzer
        | Upload
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
    let currentPage = Var.Create Home

    let homeDoc =
        currentPage.View
        |> Doc.BindView (fun p ->
            if p = Home then
                div [] [ h2 [] [ text "Home" ] ]
            else Doc.Empty
        )

    let analyzerDoc =
        currentPage.View
        |> Doc.BindView (fun p ->
            if p = Analyzer then
                div [] [ h2 [] [ text "Analyzer" ] ]
            else Doc.Empty
        )
    open WebSharper.JavaScript

    let initFileUpload () =
        let input = JS.Document.GetElementById("fileInput")

        input?onchange <- fun _ ->
            if input?files?length > 0 then
                let file = input?files?("0")

                let reader = JS.New(JS.Global?FileReader)

                reader?onload <- fun _ ->
                    let content = reader?result
                    fileContent.Value <- content
                    let parsed = parseGCode content

                    JS.Global?console?log(content)

                reader?readAsText(file)

    let uploadDoc =
        currentPage.View
        |> Doc.BindView (fun p ->
            if p = Upload then
                div [] [
                    h2 [] [ text "Upload CNC file" ]
                ]
            else Doc.Empty
        )
    let openFileDialog () =
        let input = JS.Document.GetElementById("fileInput")
        input?click()

    [<SPAEntryPoint>]
    let Main () =

        IndexTemplate.Main()

            // Menü
            .GoHome(fun _ -> currentPage.Value <- Home)
            .GoAnalyzer(fun _ -> currentPage.Value <- Analyzer)
            .GoUpload(fun _ -> 
                openFileDialog()
                currentPage.Value <- Upload
            )

            // 🔥 EZ A KULCS
            .HomeView(homeDoc)
            .AnalyzerView(analyzerDoc)
            .UploadView(uploadDoc)
            

            .Doc()
        |> Doc.RunById "main"
        initFileUpload()