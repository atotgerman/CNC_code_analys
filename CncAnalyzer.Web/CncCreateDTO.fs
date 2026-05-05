namespace CncAnalyzer.Web.CncCreateDto
open WebSharper

[<JavaScript>]
type CncCreateDto = {
    Name: string
    Turning: string
    GCode: string
}