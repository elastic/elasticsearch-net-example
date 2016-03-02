#load "Types.fsx"

open System
open System.Xml.Serialization
open System.IO

open Types

let serializer = new XmlSerializer(typeof<NugetDump>)

let getFiles path =
    Directory.GetFiles(path, "nugetdump-*.xml")

let readFile file =
    use fileInfo = File.OpenText(file)
    serializer.Deserialize(fileInfo) :?> NugetDump

let getDumps path =
    path
    |> getFiles
    |> Seq.map readFile

//getDumps (__SOURCE_DIRECTORY__ + "/data")
//|> Seq.iter (printfn "%A")
