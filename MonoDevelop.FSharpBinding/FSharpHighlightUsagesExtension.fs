namespace MonoDevelop.FSharp

open System
open Mono.TextEditor
open MonoDevelop
open MonoDevelop.Core
open MonoDevelop.Ide
open MonoDevelop.Ide.FindInFiles
open MonoDevelop.Ide.Editor.Extension
open MonoDevelop.Projects
open ICSharpCode.NRefactory.Semantics
open ICSharpCode.NRefactory.TypeSystem
open ICSharpCode.NRefactory.TypeSystem.Implementation
open Microsoft.FSharp.Compiler.SourceCodeServices
open FSharp.CompilerBinding

/// MD/XS extension for highlighting the usages of a symbol within the current buffer.
type HighlightUsagesExtension() =
    inherit AbstractUsagesExtension<(string * FSharpSymbolUse []) option>()

    override x.Initialize() =
        base.Initialize ()
        let syntaxMode = new FSharpSyntaxMode (x.Editor, x.DocumentContext)
        x.Editor.SemanticHighlighting <- syntaxMode

    override x.ResolveAsync (token) =
        Async.StartAsTask (
            cancellationToken = token,
            computation = async {
            try
                let line, col, lineStr = x.Editor.GetLineInfoByCaretOffset ()
                let currentFile = x.DocumentContext.Name
                let source = x.Editor.Text

                let projectFilename, files, args = MonoDevelop.getCheckerArgs(x.DocumentContext.Project, currentFile)

                let! symbolReferences = MDLanguageService.Instance.GetUsesOfSymbolAtLocationInFile (projectFilename, currentFile, source, files, line, col, lineStr, args)
                return symbolReferences
            with
            | :? Threading.Tasks.TaskCanceledException -> return None
            | exn -> LoggingService.LogError("Unhandled Exception in F# HighlightingUsagesExtension", exn)
                     return None })
            
    override x.GetReferences(resolveResult, token) =
        if token.IsCancellationRequested then Seq.empty else
            try
                match resolveResult with
                | Some(fsSymbolName, references) ->
                    //TODO: Can we use the DisplayName from the symbol rather than the last element in ident islands?
                    // If we could then we could remove the Parsing.findLongIdents in  GetUsesOfSymbolAtLocationInFile.
                    references
                    |> Seq.map (fun symbolUse -> NRefactory.createMemberReference(x.Editor, x.DocumentContext, symbolUse, fsSymbolName))
                | _ -> Seq.empty
                                
            with
            | :? Threading.Tasks.TaskCanceledException -> Seq.empty
            | exn -> LoggingService.LogError("Unhandled Exception in F# HighlightingUsagesExtension", exn)
                     Seq.empty

    override x.Dispose () =
        x.Editor.SemanticHighlighting.Dispose()
        x.Editor.SemanticHighlighting <- null
