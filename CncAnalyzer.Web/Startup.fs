open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open WebSharper.AspNetCore
open CncAnalyzer.Web

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    
    // Add services to the container.
    builder.Services.AddWebSharper()
        .AddAuthentication("WebSharper")
        .AddCookie("WebSharper", fun options -> ())
    |> ignore

    builder.Services.AddEndpointsApiExplorer() |> ignore
    builder.Services.AddSwaggerGen() |> ignore

    let app = builder.Build()

    Server.initializeDatabase()
    app.UseSwagger() |> ignore
    app.UseSwaggerUI() |> ignore

    // Configure the HTTP request pipeline.
    if not (app.Environment.IsDevelopment()) then
        app.UseExceptionHandler("/Error")
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            .UseHsts()
        |> ignore

    app.MapGet("/api/cnc", Func<_>(fun () ->
        Server.getAllCncFiles()
    ))
    |> ignore

    app.UseHttpsRedirection()
//#if DEBUG        
//        .UseWebSharperScriptRedirect(startVite = false)
//#endif
        .UseDefaultFiles()
        .UseStaticFiles()
        //Enable if you want to make RPC calls to server
        .UseWebSharperRemoting()
    |> ignore 
       
    app.Run()

    0 // Exit code
