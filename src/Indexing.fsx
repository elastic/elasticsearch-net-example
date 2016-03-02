#load "References.fsx"
#load "Dumper.fsx"
#load "Types.fsx"

open Nest
open Dumper
open System
open Types

module Configuration =
    let connectionSettings =
        let connSettings =
            new ConnectionSettings(new Uri("http://192.168.99.100:9200"))
        connSettings.DefaultIndex("nusearch")

    let getClient() =
        new ElasticClient(connectionSettings)

let indexPackages<'T when 'T: not struct> (client:ElasticClient) (package:'T) =
    let indexSomething = new IndexRequest<'T>(DocumentPath package)
    let result = client.Index(package)
    if result.IsValid then printfn " ==> Done"
    else
        printfn " ==> Error: %A" (result.CallDetails.OriginalException.Message)
        printfn " ==> Error: %A" (result)
        printfn " ==> Package: %A" package

let indexDumps count =
    let client = Configuration.getClient()

    getDumps (__SOURCE_DIRECTORY__ + "/data")
    |> Seq.map (fun x -> x.NugetPackages)
    |> Seq.concat
    |> Seq.take count
    |> Seq.iter (indexPackages client)

indexDumps 10
