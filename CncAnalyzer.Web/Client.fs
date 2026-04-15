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

    type Page =
        | Home
        | Analyzer
        | Upload

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

    [<SPAEntryPoint>]
    let Main () =

        IndexTemplate.Main()

            // Menü
            .GoHome(fun _ -> currentPage.Value <- Home)
            .GoAnalyzer(fun _ -> currentPage.Value <- Analyzer)
            .GoUpload(fun _ -> currentPage.Value <- Upload)

            // 🔥 EZ A KULCS
            .HomeView(homeDoc)
            .AnalyzerView(analyzerDoc)
            .UploadView(uploadDoc)
            

            .Doc()
        |> Doc.RunById "main"
        initFileUpload()