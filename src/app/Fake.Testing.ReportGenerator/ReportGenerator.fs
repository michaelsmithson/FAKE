/// Contains a task which can be used to run [ReportGenerator](https://github.com/danielpalme/ReportGenerator),
/// which converts XML reports generated by PartCover, OpenCover or NCover into a readable report in various formats.
///
/// ## Sample
/// 
/// ```
/// open Fake.Testing
///
/// Target.create "Generate Reports" (fun _ ->
///   let parameters p = { p with TargetDir = "c:/reports/" }
///   !! "**/opencover.xml"
///   |> ReportGenerator.generateReports parameters
/// )
/// ```
[<RequireQualifiedAccess>]
module Fake.Testing.ReportGenerator

open System
open System.Text
open System.IO

open Fake.Core
open Fake.IO

type ReportType =
    | Html = 0
    | HtmlSummary = 1
    | Xml = 2
    | XmlSummary = 3
    | Latex = 4
    | LatexSummary = 5
    | Badges = 6

type LogVerbosity =
    | Verbose = 0
    | Info = 1
    | Error = 2

/// ReportGenerator parameters, for more details see: https://github.com/danielpalme/ReportGenerator.
type ReportGeneratorParams =
    { /// (Required) Path to the ReportGenerator exe file.
      ExePath : string
      /// (Required) The directory where the generated report should be saved.
      TargetDir : string
      /// The output formats and scope.
      ReportTypes : ReportType list
      /// Optional directories which contain the corresponding source code.
      SourceDirs : string list
      /// Optional directory for storing persistent coverage information.
      /// Can be used in future reports to show coverage evolution.
      HistoryDir : string
      /// Optional list of assemblies that should be included or excluded
      /// in the report. Exclusion filters take precedence over inclusion
      /// filters. Wildcards are allowed.
      Filters : string list
      /// The verbosity level of the log messages.
      LogVerbosity : LogVerbosity
      /// The directory where the ReportGenerator process will be started.
      WorkingDir : string
      /// The timeout for the ReportGenerator process.
      TimeOut : TimeSpan }

let private currentDirectory = Directory.GetCurrentDirectory ()

/// ReportGenerator default parameters
let private ReportGeneratorDefaultParams =
    { ExePath = "./tools/ReportGenerator/bin/ReportGenerator.exe"
      TargetDir = currentDirectory
      ReportTypes = [ ReportType.Html ]
      SourceDirs = []
      HistoryDir = String.Empty
      Filters = []
      LogVerbosity = LogVerbosity.Verbose
      WorkingDir = currentDirectory
      TimeOut = TimeSpan.FromMinutes 5. }

/// Builds the report generator command line arguments from the given parameters and reports
/// [omit]
let private buildReportGeneratorArgs parameters (reports : string seq) =
    let reportTypes = parameters.ReportTypes |> List.map (fun rt -> rt.ToString())
    let sourceDirs = sprintf "-sourcedirs:%s" (String.Join(";", parameters.SourceDirs))
    let filters = sprintf "-filters:%s" (String.Join(";", parameters.Filters))

    new StringBuilder()
    |> StringBuilder.append (sprintf "-reports:%s" (String.Join(";", reports)))
    |> StringBuilder.append (sprintf "-targetdir:%s" parameters.TargetDir)
    |> StringBuilder.appendWithoutQuotes (sprintf "-reporttypes:%s" (String.Join(";", reportTypes)))
    |> StringBuilder.appendIfTrue (parameters.SourceDirs.Length > 0) sourceDirs
    |> StringBuilder.appendStringIfValueIsNotNullOrEmpty (parameters.HistoryDir) (sprintf "-historydir:%s" parameters.HistoryDir)
    |> StringBuilder.appendIfTrue (parameters.Filters.Length > 0) filters
    |> StringBuilder.appendWithoutQuotes (sprintf "-verbosity:%s" (parameters.LogVerbosity.ToString()))
    |> StringBuilder.toText

/// Runs ReportGenerator on one or more coverage reports.
/// ## Parameters
///
///  - `setParams` - Function used to overwrite the default ReportGenerator parameters.
///  - `reports` - Coverage reports.
let generateReports setParams (reports : string list) =
    let taskName = "ReportGenerator"
    let description = "Generating reports"
    
    use __ = Trace.traceTask taskName description
    let param = setParams ReportGeneratorDefaultParams

    let processArgs = buildReportGeneratorArgs param reports
    Trace.tracefn "ReportGenerator command\n%s %s" param.ExePath processArgs

    let processStartInfo info = 
      { info with FileName = param.ExePath
                  WorkingDirectory = if param.WorkingDir |> String.isNullOrEmpty then info.WorkingDirectory else param.WorkingDir
                  Arguments = processArgs } |> Process.withFramework
    match Process.execSimple processStartInfo param.TimeOut with
    | 0 -> ()
    | v -> failwithf "ReportGenerator reported errors: %i" v