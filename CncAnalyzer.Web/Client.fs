namespace CncAnalyzer.Web

open WebSharper
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.UI.Html
open WebSharper.JavaScript
open CncAnalyzer.Web.Parser
open WebSharper.JavaScript.Dom

[<JavaScript>]
module Client =

    type IndexTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>
    let fileContent = Var.Create ""
    type Page =
        | Home
        | Analyzer
        | Upload
    
    type Direction = {
        Angle: float
        Length: float
    }

    type Point = {
        X: float
        Y: float
    }


    let directionsVar : Var<Direction[]> = Var.Create [||]
    let drawCompass (canvas: HTMLCanvasElement) (dirs: Direction[]) =
        let ctx = canvas.GetContext("2d")

        canvas.Width <- 400
        canvas.Height <- 400

        ctx.ClearRect(0., 0., 400., 400.)

        let centerX = 200.0
        let centerY = 200.0
        let radius = 150.0

    // 🔵 Kör (kompasz keret)
        ctx.StrokeStyle <- "white"
        ctx.LineWidth <- 1.0
        ctx.BeginPath()
        ctx.Arc(centerX, centerY, radius, 0., 2.0 * System.Math.PI)
        ctx.Stroke()

    // 🔵 Tengelyek
        ctx.BeginPath()
        ctx.MoveTo(centerX - radius, centerY)
        ctx.LineTo(centerX + radius, centerY)
        ctx.MoveTo(centerX, centerY - radius)
        ctx.LineTo(centerX, centerY + radius)
        ctx.Stroke()

    // 🔴 Max hossz (normalizáláshoz)
        let maxLen =
            dirs
            |> Array.map (fun d -> d.Length)
            |> Array.max
        let maxLog = System.Math.Log(1.0 + maxLen)

        ctx.StrokeStyle <- "red"
        ctx.LineWidth <- 2.0

    // 🔴 Irányok rajzolása (normalizált)
        for d in dirs do
            let norm =
                let l = System.Math.Log(1.0 + d.Length)
                l / maxLog
            let r = norm * radius

            let x = centerX + cos(d.Angle) * r
            let y = centerY - sin(d.Angle) * r

            ctx.BeginPath()
            ctx.MoveTo(centerX, centerY)
            ctx.LineTo(x, y)
            ctx.Stroke()
    let computeDirections (lines: Parser.GCodeLine[]) =
        lines
        |> Array.pairwise
        |> Array.choose (fun (a, b) ->
            match a.X, a.Y, b.X, b.Y with
            | Some x1, Some y1, Some x2, Some y2 ->
                let dx = x2 - x1
                let dy = y2 - y1

                let angle = System.Math.Atan2(dy, dx)
                let length = System.Math.Sqrt(dx * dx + dy * dy)

                Some { Angle = angle; Length = length }
            | _ -> None
    )

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
            div [] [
                h2 [] [ text "Analyzer" ]

                canvas [
                    attr.id "compassCanvas"
                    attr.width "400"
                    attr.height "400"
                    on.afterRender (fun el ->
                        let canvas = el :?> HTMLCanvasElement

                        directionsVar.View
                        |> View.Sink (fun dirs ->
                            if dirs.Length > 0 then
                                drawCompass canvas dirs
                        )
                        JS.Global?console?log("RAJZOLTAM")
                    )
                ] []
            ] 
        else Doc.Empty
    )

    let initFileUpload () =
        let input = JS.Document.GetElementById("fileInput")

        input?onchange <- fun _ ->
            if input?files?length > 0 then
                JS.Global?console?log("CHANGE FIRED")
                let file = input?files?("0")
                JS.Global?console?log("FILE OK")
                let reader = JS.New(JS.Global?FileReader)

                reader?onload <- fun _ ->
                    JS.Global?console?log("ONLOAD FIRED")
                    let content = string (reader?result)   // 🔥 fontos: string!
                    fileContent.Value <- content

                    let parsed = Parser.parseGCode content
                    let dirs = computeDirections parsed

                    directionsVar.Value <- dirs

                    JS.Global?console?log(dirs)

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