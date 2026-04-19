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
    let zoomVar = Var.Create 1.0
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

    let offsetVar = Var.Create (0.0, 0.0)
    let directionsVar : Var<Direction[]> = Var.Create [||]
    let drawCompass (canvas: HTMLCanvasElement) (dirs: Direction[]) =
        let ctx = canvas.GetContext("2d")
        let zoom = zoomVar.Value
        let (offX, offY) = offsetVar.Value
        canvas.Width <- 400
        canvas.Height <- 400

        ctx.ClearRect(0., 0., 400., 400.)

        let centerX = 200.0
        let centerY = 200.0
        let radius = 150.0

        let transform (x: float) (y: float) =
            let tx = centerX + (x - centerX) * zoom + offX
            let ty = centerY + (y - centerY) * zoom + offY
            tx, ty
        let cx, cy = transform centerX centerY
    // 🔵 Kör (kompasz keret)
        ctx.StrokeStyle <- "white"
        ctx.LineWidth <- 1.0
        ctx.BeginPath()
        ctx.Arc(cx, cy, radius * zoom , 0., 2.0 * System.Math.PI)
        ctx.Stroke()

    // 🔵 Tengelyek
        ctx.BeginPath()
        let x1, y1 = transform (centerX - radius) centerY
        let x2, y2 = transform (centerX + radius) centerY
        ctx.MoveTo(x1, y1)
        ctx.LineTo(x2, y2)
        let x3, y3 = transform centerX (centerY - radius)
        let x4, y4 = transform centerX (centerY + radius)
        ctx.MoveTo(x3,y3)
        ctx.LineTo(x4,y4)
        ctx.Stroke()
        ctx.FillStyle <- "white"
        ctx.Font <- "14px sans-serif"

        let nx, ny = transform centerX (centerY - radius - 30.0)
        let sx, sy = transform centerX (centerY + radius + 40.0)
        let ex, ey = transform (centerX + radius + 30.0) centerY
        let wx, wy = transform (centerX - radius - 40.0) centerY

        ctx.FillText("N", nx, ny)
        ctx.FillText("S", sx,sy)
        ctx.FillText("E", ex,ey)
        ctx.FillText("W", wx, wy)

        for deg in 0 .. 15 .. 345 do
            let rad = float deg * System.Math.PI / 180.0

            let outerX = centerX + cos(rad) * radius
            let outerY = centerY - sin(rad) * radius

            let innerX = centerX + cos(rad) * (radius - 10.0)
            let innerY = centerY - sin(rad) * (radius - 10.0)

    // tick vonal
            let x1, y1 = transform innerX innerY
            let x2, y2 = transform outerX outerY
            ctx.BeginPath()
            ctx.MoveTo(x1, y1)
            ctx.LineTo(x2, y2)
            ctx.Stroke()

    // csak fő szögek felirata
            if deg % 45 = 0 then
                let textX = centerX + cos(rad) * (radius + 20.0)
                let textY = centerY - sin(rad) * (radius + 20.0)

                let tx2, ty2 = transform textX textY
                ctx.FillText(string deg, tx2 - 10.0, ty2 + 5.0)

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
            let baseX = centerX + cos(d.Angle) * r
            let baseY = centerY - sin(d.Angle) * r
            let x = centerX + (cos(d.Angle) * r * zoom) + offX
            let y = centerY - (sin(d.Angle) * r * zoom) + offY

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

                        canvas.AddEventListener("wheel", fun (ev: Dom.Event)  ->
                            let e = ev :?> Dom.WheelEvent
                            e.PreventDefault()

                            let factor =
                                if e.DeltaY < 0 then 1.1 else 0.9

                            zoomVar.Value <- zoomVar.Value * factor
                        )

                        View.Map2 (fun dirs zoom -> dirs) directionsVar.View zoomVar.View
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