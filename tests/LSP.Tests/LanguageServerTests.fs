module LSP.LanguageServerTests

open System
open System.IO 
open System.Text
open SimpleTest
open LSP.Json
open LSP.Types

let binaryWriter () = 
    let stream = new MemoryStream()
    let writer = new BinaryWriter(stream)
    let toString () = 
        let bytes = stream.ToArray() 
        Encoding.UTF8.GetString bytes
    (writer, toString)

let ``test write text`` (t: TestContext) = 
    let (writer, toString) = binaryWriter() 
    writer.Write (Encoding.UTF8.GetBytes "foo")
    let found = toString()
    if found <> "foo" then Fail(found)

let ``test write response`` (t: TestContext) = 
    let (writer, toString) = binaryWriter() 
    LanguageServer.respond writer 1 "2"
    let expected = "Content-Length: 19\r\n\r\n\
                    {\"id\":1,\"result\":2}"
    let found = toString()
    if found <> expected then Fail(found)

let ``test write multibyte characters`` (t: TestContext) = 
    let (writer, toString) = binaryWriter() 
    LanguageServer.respond writer 1 "🔥"
    let expected = "Content-Length: 22\r\n\r\n\
                    {\"id\":1,\"result\":🔥}"
    let found = toString()
    if found <> expected then Fail(found)

let TODO() = raise (Exception "TODO")

type MockServer() = 
    interface ILanguageServer with 
        member this.Initialize(p: InitializeParams): Async<InitializeResult> = 
            async {
                return { capabilities = defaultServerCapabilities }
            }
        member this.Initialized(): Async<unit> = TODO() 
        member this.Shutdown(): Async<unit> = TODO() 
        member this.DidChangeConfiguration(p: DidChangeConfigurationParams): Async<unit>  = TODO()
        member this.DidOpenTextDocument(p: DidOpenTextDocumentParams): Async<unit>  = TODO()
        member this.DidChangeTextDocument(p: DidChangeTextDocumentParams): Async<unit>  = TODO()
        member this.WillSaveTextDocument(p: WillSaveTextDocumentParams): Async<unit> = TODO()
        member this.WillSaveWaitUntilTextDocument(p: WillSaveTextDocumentParams): Async<TextEdit list> = TODO()
        member this.DidSaveTextDocument(p: DidSaveTextDocumentParams): Async<unit> = TODO()
        member this.DidCloseTextDocument(p: DidCloseTextDocumentParams): Async<unit> = TODO()
        member this.DidChangeWatchedFiles(p: DidChangeWatchedFilesParams): Async<unit> = TODO()
        member this.Completion(p: TextDocumentPositionParams): Async<CompletionList option> = TODO()
        member this.Hover(p: TextDocumentPositionParams): Async<Hover option> = TODO()
        member this.ResolveCompletionItem(p: CompletionItem): Async<CompletionItem> = TODO()
        member this.SignatureHelp(p: TextDocumentPositionParams): Async<SignatureHelp option> = TODO()
        member this.GotoDefinition(p: TextDocumentPositionParams): Async<Location list> = TODO()
        member this.FindReferences(p: ReferenceParams): Async<Location list> = TODO()
        member this.DocumentHighlight(p: TextDocumentPositionParams): Async<DocumentHighlight list> = TODO()
        member this.DocumentSymbols(p: DocumentSymbolParams): Async<SymbolInformation list> = TODO()
        member this.WorkspaceSymbols(p: WorkspaceSymbolParams): Async<SymbolInformation list> = TODO()
        member this.CodeActions(p: CodeActionParams): Async<Command list> = TODO()
        member this.CodeLens(p: CodeLensParams): Async<CodeLens list> = TODO()
        member this.ResolveCodeLens(p: CodeLens): Async<CodeLens> = TODO()
        member this.DocumentLink(p: DocumentLinkParams): Async<DocumentLink list> = TODO()
        member this.ResolveDocumentLink(p: DocumentLink): Async<DocumentLink> = TODO()
        member this.DocumentFormatting(p: DocumentFormattingParams): Async<TextEdit list> = TODO()
        member this.DocumentRangeFormatting(p: DocumentRangeFormattingParams): Async<TextEdit list> = TODO()
        member this.DocumentOnTypeFormatting(p: DocumentOnTypeFormattingParams): Async<TextEdit list> = TODO()
        member this.Rename(p: RenameParams): Async<WorkspaceEdit> = TODO()
        member this.ExecuteCommand(p: ExecuteCommandParams): Async<unit> = TODO()
        member this.DidChangeWorkspaceFolders(p: DidChangeWorkspaceFoldersParams): Async<unit> = TODO()

let messageStream (messages: string list): BinaryReader = 
    let stdin = new MemoryStream()
    for m in messages do 
        let length = Encoding.UTF8.GetByteCount m 
        let wrapper = sprintf "Content-Length: %d\r\n\r\n%s" length m 
        let bytes = Encoding.UTF8.GetBytes wrapper 
        stdin.Write(bytes, 0, bytes.Length)
    stdin.Seek(int64 0, SeekOrigin.Begin) |> ignore
    new BinaryReader(stdin)

let initializeMessage = """
{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {}
}
"""

let ``test read messages from a stream`` (t: TestContext) = 
    let stdin = messageStream [initializeMessage]
    let messages = LanguageServer.readMessages stdin
    let found = Seq.toList messages
    if found <> [Parser.RequestMessage (1, "initialize", JsonValue.Parse "{}")] then Fail(found)

let exitMessage = """
{
    "jsonrpc": "2.0",
    "method": "exit"
}
"""
    
let ``test exit message terminates stream`` (t: TestContext) = 
    let stdin = messageStream [initializeMessage; exitMessage; initializeMessage]
    let messages = LanguageServer.readMessages stdin
    let found = Seq.toList messages
    if found <> [Parser.RequestMessage (1, "initialize", JsonValue.Parse "{}")] then Fail(found)
    
let ``test end of bytes terminates stream`` (t: TestContext) = 
    let stdin = messageStream [initializeMessage]
    let messages = LanguageServer.readMessages stdin
    let found = Seq.toList messages
    if found <> [Parser.RequestMessage (1, "initialize", JsonValue.Parse "{}")] then Fail(found)

let mock (server: ILanguageServer) (messages: string list): string = 
    let stdout = new MemoryStream()
    let writeOut = new BinaryWriter(stdout)
    let readIn = messageStream messages
    LanguageServer.connect (fun _ -> server) readIn writeOut
    Encoding.UTF8.GetString(stdout.ToArray())

let ``test send Initialize`` (t: TestContext) = 
    let message = """
    {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "initialize",
        "params": {"processId": null,"rootUri":null,"capabilities":{}}
    }
    """
    let server = MockServer()
    let result = mock server [message]
    if not (result.Contains("capabilities")) then Fail(result)