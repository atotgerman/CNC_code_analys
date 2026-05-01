namespace CncAnalyzer.Web

open WebSharper
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Templating
open WebSharper.UI.Html
open WebSharper.JavaScript
open CncAnalyzer.Web.Parser
open WebSharper.JavaScript.Dom
open CncAnalyzer.Web.Server
open System.Text.Json

[<JavaScript>]
module Client =
    let zoomVar = Var.Create 1.0
    type Cmd =
        | Rapid of float*float
        | Line of float*float
        | ArcCW of float*float
    
    let gcodeVar : Var<Cmd[]> = Var.Create [||]
    type IndexTemplate = Template<"wwwroot/index.html", ClientLoad.FromDocument>
    let fileContent = Var.Create ""
    type Page =
        | Home
        | Analyzer
        | Upload
        | Save
    
    type Direction = {
        Angle: float
        Length: float
    }

    type Point = {
        X: float
        Y: float
    }
    let showFormVar = Var.Create false
    let nameVar = Var.Create ""
    let turningVar = Var.Create ""
    let saveCanvasAsImage (canvasId: string) =
        let canvas = JS.Document.GetElementById(canvasId) :?> HTMLCanvasElement
        let dataUrl = canvas.ToDataURL("image/png")

        let link = JS.Document.CreateElement("a")
        link?href <- dataUrl
        link?download <- canvasId + ".png"
        link?click()
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
    let circleFrom3Points (p1: float*float) (p2: float*float) (p3: float*float) =
        let (x1,y1) = p1
        let (x2,y2) = p2
        let (x3,y3) = p3

        let a = x2 - x1
        let b = y2 - y1
        let c = x3 - x1
        let d = y3 - y1

        let e = a*(x1+x2) + b*(y1+y2)
        let f = c*(x1+x3) + d*(y1+y3)
        let g = 2.0*(a*(y3-y2) - b*(x3-x2))

        if abs g < 1e-6 then None
        else
            let cx = (d*e - b*f) / g
            let cy = (a*f - c*e) / g
            let r = sqrt((cx-x1)**2.0 + (cy-y1)**2.0)
            Some (cx, cy, r)

    let drawGCodeReal (canvas: HTMLCanvasElement) (cmds: Cmd[]) =
        let ctx = canvas.GetContext("2d")

        canvas.Width <- 600
        canvas.Height <- 600
        ctx.ClearRect(0.,0.,600.,600.)

        let centerX = 300.0
        let centerY = 300.0

        // 🔵 összes pont kigyűjtése bounding boxhoz
        let pts =
            cmds
            |> Array.map (function
                | Rapid(x,y) | Line(x,y) | ArcCW(x,y) -> (x,y))

        let pxPerMm = zoomVar.Value

        let transform (x,y) =
            let tx = centerX + x * pxPerMm + fst offsetVar.Value
            let ty = centerY - y * pxPerMm + snd offsetVar.Value
            tx, ty

        // ⚪ tengelyek
        ctx.StrokeStyle <- "#555"
        ctx.BeginPath()
        ctx.MoveTo(0.,centerY)
        ctx.LineTo(600.,centerY)
        ctx.MoveTo(centerX,0.)
        ctx.LineTo(centerX,600.)
        ctx.Stroke()

        ctx.FillStyle <- "#aaa"
        ctx.Font <- "10px monospace"

        let step = 25.0

        for i in -50 .. 50 do
            let x = float i * step
            let tx, _ = transform(x, 0.0)
            ctx.FillText(string x, tx + 2.0, centerY + 12.0)

            let y = float i * step
            let _, ty = transform(0.0, y)
            ctx.FillText(string y, centerX + 4.0, ty - 2.0)

        ctx.StrokeStyle <- "#333"
        ctx.LineWidth <- 1.0

        let step = 10.0  // 10 mm

        for i in -50 .. 50 do
            let x = float i * step
            let x1,y1 = transform(x, -500.0)
            let x2,y2 = transform(x, 500.0)

            ctx.BeginPath()
            ctx.MoveTo(x1,y1)
            ctx.LineTo(x2,y2)
            ctx.Stroke()

            let y = float i * step
            let x3,y3 = transform(-500.0, y)
            let x4,y4 = transform(500.0, y)

            ctx.BeginPath()
            ctx.MoveTo(x3,y3)
            ctx.LineTo(x4,y4)
            ctx.Stroke()

        // 🟢 rajzolás
        ctx.StrokeStyle <- "lime"
        ctx.LineWidth <- 2.0

        let mutable prev = (0.0,0.0)

        let rec loop i =
            if i < cmds.Length then
                match cmds.[i] with

                // G0
                | Rapid(x,y) ->
                    prev <- (x,y)
                    loop (i+1)

                // G1
                | Line(x,y) ->
                    let x1,y1 = transform prev
                    let x2,y2 = transform (x,y)

                    ctx.BeginPath()
                    ctx.MoveTo(x1,y1)
                    ctx.LineTo(x2,y2)
                    ctx.Stroke()

                    prev <- (x,y)
                    loop (i+1)

                // G2 (ív)
                | ArcCW(x,y) ->
                    if i >= 1 && i+1 < cmds.Length then
                        let p1 = prev
                        let p2 = (x,y)
                        let p3 =
                            match cmds.[i+1] with
                            | ArcCW(x3,y3) -> (x3,y3)
                            | _ -> (x,y)

                        match circleFrom3Points p1 p2 p3 with
                        | Some(cx,cy,r) ->

                            let (tcx,tcy) = transform (cx,cy)
                            let (sx,sy) = transform p1
                            let (ex,ey) = transform p2

                            let startAng = atan2 (sy - tcy) (sx - tcx)
                            let endAng   = atan2 (ey - tcy) (ex - tcx)

                            ctx.BeginPath()
                            ctx.Arc(tcx,tcy,r*pxPerMm,startAng,endAng,true)
                            ctx.Stroke()

                        | None -> ()

                    prev <- (x,y)
                    loop (i+1)

        loop 0
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
    let saveDoc =
        currentPage.View
        |> Doc.BindView (fun p ->
            if p = Save then
                div [ attr.``class`` "p-6 flex flex-col gap-4" ] [

                    h2 [] [ text "Mentés adatbázisba" ]

                    

                    Doc.InputType.Text [
                        attr.placeholder "Név"
                        attr.``class`` "p-2 text-black"
                    ] nameVar

                    Doc.InputType.Text [
                        attr.placeholder "Forgácsolás"
                        attr.``class`` "p-2 text-black"
                    ] turningVar

                    button [
                        attr.``class`` "px-4 py-2 bg-green-600 rounded"
                        on.click (fun _ _ ->
                            async {
                                let name = nameVar.Value
                                let turning= turningVar.Value
                                
                                do! SaveCncRpc name turning fileContent.Value

                                JS.Global?console?log("SAVE:", name, turning)
                                JS.Global?console?log("MENTVE!")
                            } |> Async.StartImmediate
                            
                        )
                    ] [ text "Mentés" ]
                ]
            else Doc.Empty
        )
    let homeDoc =
        currentPage.View
        |> Doc.BindView (fun p ->
            if p = Home then
                div [] [ h2 [] [ text "Home" ] ]
            else Doc.Empty
        )
    let toggleForm () =
        showFormVar.Value <- not showFormVar.Value
    let analyzerDoc =
        currentPage.View
        |> Doc.BindView (fun p ->
        if p = Analyzer then
            div [] [
                h2 [] [ text "Analyzer" ]
                div [ attr.``class`` "flex flex-col gap-6" ] [
                    canvas [
                        attr.id "compassCanvas"
                        attr.width "600"
                        attr.height "600"

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

                          
                        )
                    ] []
                    canvas [
                        attr.id "pathCanvas"
                        attr.width "600"
                        attr.height "600"

                        on.afterRender (fun el ->
                            let canvas = el :?> HTMLCanvasElement

                            View.Map2 (fun cmds zoom -> cmds) gcodeVar.View zoomVar.View
                                |> View.Sink (fun cmds ->
                                if cmds.Length > 0 then
                                    drawGCodeReal canvas cmds
                            )
                        )
                    ] []


                    div [ attr.``class`` "flex gap-4 pt-4" ] [

                        // Path mentés
                        gcodeVar.View
                        |> View.Map (fun cmds ->
                            if cmds.Length > 0 then
                                button [
                                    attr.``class`` "px-4 py-2 bg-green-600 hover:bg-green-500 rounded"
                                    on.click (fun _ -> saveCanvasAsImage "pathCanvas")
                                ] [ text "Save Path as PNG" ]
                            else Doc.Empty
                        )
                        |> Doc.EmbedView

                        // Compass mentés
                        directionsVar.View
                        |> View.Map (fun dirs ->
                            if dirs.Length > 0 then
                                button [
                                    attr.``class`` "px-4 py-2 bg-blue-600 hover:bg-blue-500 rounded"
                                    on.click (fun _ -> saveCanvasAsImage "compassCanvas")
                                ] [ text "Save Compass as PNG" ]
                            else Doc.Empty
                        )
                        |> Doc.EmbedView
                        button [
                            attr.``class`` "px-4 py-2 bg-purple-600 hover:bg-purple-500 rounded"
                            on.click (fun _ _-> toggleForm())
                    ] [
                    textView (
                        showFormVar.View
                        |> View.Map (fun v -> if v then "Bezár" else "Mentés adatbázisba")
                    
                    )
                    ] 
                    ]
                    
                ]
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

                    let cmds =
                        parsed
                        |> Array.choose (fun l ->
                            match l.Cmd, l.X, l.Y with
                            | "G0", Some x, Some y -> Some (Rapid(x,y))
                            | "G1", Some x, Some y -> Some (Line(x,y))
                            | "G2", Some x, Some y -> Some (ArcCW(x,y))
                            | _ -> None
                        )

                    gcodeVar.Value <- cmds

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
            .GoSave(fun _ -> currentPage.Value <- Save)
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
            .SaveView(saveDoc)
            

            .Doc()
        |> Doc.RunById "main"
        initFileUpload()