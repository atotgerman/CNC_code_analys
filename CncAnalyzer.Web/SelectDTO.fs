namespace CncAnalyzer.Web.SelectDTO
open WebSharper

[<JavaScript>]
type CncFileInfo = {
    Id: int
    Name: string
    Turning: string
}