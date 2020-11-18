open System
open System.IO
open System.Collections.Generic
open System.Net
open AltoHttp
open FSharp.Json
open EasyConsoleCore
open ICSharpCode.SharpZipLib.Zip
open MingHuiQiKanTool


let affirmMingHuiCacheData () =
    let mutable data : Option<MingHuiCacheData> = None
    let mutable needUpdate : bool = true
    
    if File.Exists FILE_MINGHUI_CACHE then
        data <- Some ( Json.deserialize< MingHuiCacheData >( File.ReadAllText FILE_MINGHUI_CACHE ) )
        
        if data.IsSome then
            
            printfn "已加载缓存"
        
            let ts = DateTime.Now.Subtract data.Value.CacheTimeStamp
            
            if ts.Days > 1 then
                printfn "缓存时间与当前相差: %d天, 是否需要更新? (0-不需要, 1-需要)" ts.Days
                let input = Console.ReadLine()
                let mutable inputCode = 0
                if Int32.TryParse (input, &inputCode) then
                    match inputCode with
                    | 0 -> needUpdate <- false
                    | _ -> needUpdate <- true
                else
                   needUpdate <- false 
                    
            else
                printfn "缓存有效"
                
                needUpdate <- false
            
    if needUpdate then
        data <- Some ( fetchLatestData )
        File.WriteAllText ( FILE_MINGHUI_CACHE, Json.serialize data.Value )

    for bookList in data.Value.BookList do
        printfn "===== %s =====" bookList.BookInfo.BookTypeName
        for bookInfo in bookList.BookList do
            printfn "%A %A %A" bookInfo.BookTitle bookInfo.BookPublishDate bookInfo.BooksUrl
        printfn "=============="
    
    data


let unzip (path : string) (outputName : string) =
    
    printfn "开始处理压缩文件: %s 输出: %s" path outputName
    
    try
        let unzippedFiles = List<string>()

        using ( ZipInputStream(File.OpenRead(path)) ) ( fun zipInputStream ->
            let mutable zipEntry : ZipEntry = null
            zipEntry <- zipInputStream.GetNextEntry ()
            while zipEntry <> null do
                if Path.GetExtension( zipEntry.Name ) = ".pdf" then
                    if zipEntry.CompressedSize <> (int64 0) then

                        unzippedFiles.Add zipEntry.Name
                        
                        let streamWriter = File.Create zipEntry.Name
                        
                        let mutable size = 4096;
                        let mutable data = Array.zeroCreate 4096
                        size <- zipInputStream.Read(data, 0, data.Length)
                        while size > 0 do
                            streamWriter.Write(data, 0, size)
                            size <- zipInputStream.Read(data, 0, data.Length)
                        streamWriter.Close ()
                        
                        printfn "%s -> %s" path zipEntry.Name

                        zipEntry <- zipInputStream.GetNextEntry ()
        )
        
        if unzippedFiles.Count <> 0 then
            for i = 0 to unzippedFiles.Count - 1 do
                let info = FileInfo ( unzippedFiles.[i] )
                if i = 0 then
                    info.MoveTo (outputName + Path.GetExtension (unzippedFiles.[i]))
                else
                    info.MoveTo ((sprintf "%s_%d" outputName (i + 1)) + Path.GetExtension (unzippedFiles.[i]))
        else
            printfn "无文件被解压"

        Output.WriteLine ( ConsoleColor.Gray, sprintf "清理压缩文件: %s" path )
        
        // 完成解压删除压缩文件
        File.Delete path
    with error -> Output.WriteLine ( ConsoleColor.Red, sprintf "解压文件时出现错误: %s" path )

    ()


let mutable data : Option<MingHuiCacheData> = None

let downloadQueue = DownloadQueue ()


type MainPage ( program : Program ) =
    inherit MenuPage( "明慧期刊工具", program,
                      Option ( "数据管理", Action ( fun () -> program.NavigateTo<MingHuiCacheDataPage>() |> ignore ) ),
                      Option ( "下载管理", Action ( fun () -> program.NavigateTo<MingHuiDownloadPage>() |> ignore ) ),
                      Option ( "退出", Action ( fun () -> Environment.Exit 0 |> ignore ) ) )
    
    override this.Display () =
        base.Display ()


and MingHuiCacheDataPage ( program : Program ) =
    inherit MenuPage( "MingHuiCacheDataPage", program,
                      
                      Option ( "核查数据", Action ( fun () ->
                          data <- affirmMingHuiCacheData ()
                          Output.WriteLine ( ConsoleColor.Green, "数据已获取, 按任意键返回" )
                          Console.ReadKey ()
                          program.NavigateTo<MainPage>() |> ignore ) ),
                      
                      Option ( "获取最新数据并保存", Action ( fun () ->
                          data <- Some ( fetchLatestData )
                          program.NavigateTo<MainPage>() |> ignore ) ) )


and MingHuiDownloadPage ( program : Program ) =
    inherit MenuPage( "MingHuiDownloadPage", program,
                      
                      Option ( "列出期刊类别列表", Action ( fun () ->
                          program.NavigateTo<BookTypeSelectPage>() |> ignore ) ),
                      
                      Option ( "下载队列", Action ( fun () ->
                          program.NavigateTo<MingHuiDownloadQueuePage>() |> ignore ) )
                       )

    override this.Display () =
        
        if data.IsNone then
            Output.WriteLine ( ConsoleColor.Red, "当前没有数据, 请先获取数据, 按任意键返回" )
            Console.ReadKey ()
            program.NavigateBack () |> ignore
        else
            base.Display ()


and MingHuiDownloadQueuePage ( program : Program ) as this =
    inherit Page( "MingHuiDownloadQueuePage", program )
    
    do
        let onQueueElementCompleted = QueueElementCompletedEventHandler ( fun sender e ->
            
            Output.WriteLine ( ConsoleColor.Green, sprintf "队列元素: %s 下载完成" e.Element.Url )
            
            if e.Element.Destination.EndsWith ".zip" then
                Output.WriteLine ( ConsoleColor.Green, sprintf "准备解压: %s" e.Element.Destination )
                unzip e.Element.Destination (Path.GetFileNameWithoutExtension(e.Element.Destination))
                
            Output.WriteLine ( ConsoleColor.Green, sprintf "队列进度: %d%s" downloadQueue.CurrentProgress "%" )
        )
        
        let onQueueCompleted = EventHandler ( fun sender e ->
            Output.WriteLine ( ConsoleColor.Cyan, sprintf "队列任务已全部完成!" )
        )
        
        let onQueueElementStartedDownloading = EventHandler ( fun sender e ->
            Output.WriteLine ( ConsoleColor.Cyan, sprintf "队列元素开始下载..." )
        )
        
        let onQueueProgressChanged = EventHandler ( fun sender e ->
            Output.WriteLine ( ConsoleColor.Cyan, sprintf "%A%s" downloadQueue.CurrentProgress "%" )
        )
        
        downloadQueue.QueueElementCompleted.AddHandler onQueueElementCompleted
        downloadQueue.QueueCompleted.AddHandler onQueueCompleted
        downloadQueue.QueueElementStartedDownloading.AddHandler onQueueElementStartedDownloading
//        downloadQueue.QueueProgressChanged.AddHandler onQueueProgressChanged
    
    member val Menu : Menu = null with get, set

    override this.Display () =
        
        this.Menu <- Menu ()
        
        if downloadQueue.QueueLength <> 0 then
            this.Menu.Add ( sprintf "当前队列进度: %d%s" downloadQueue.CurrentProgress "%", Action ( fun () ->
                      program.NavigateBack() |> ignore ) ) |> ignore
            this.Menu.Add ( sprintf "取消队列 (当前任务数:%d)" downloadQueue.QueueLength, Action ( fun () ->
                      downloadQueue.Cancel ()
                      Output.WriteLine ( ConsoleColor.Green, "已取消" )
                      Console.ReadKey () |> ignore
                      program.NavigateBack() |> ignore ) ) |> ignore
        else
            Output.WriteLine ( ConsoleColor.Cyan, "当前队列没有任务" ) |> ignore
        
        if program.NavigationEnabled && not (this.Menu.Contains("Go back")) then
            this.Menu.Add("Go back", Action (fun () -> program.NavigateBack() |> ignore)) |> ignore
        
        this.Menu.Display ()

        base.Display ()


and BookTypeSelectPage ( program : Program ) as this =
    inherit Page( "BookTypeSelectPage", program )
    
    let selectBookType ( bookType : MingHuiQiKanBookTypeInfo ) =
        Console.Clear ()
        
        let subMenu = Menu ()
        let index = List.findIndex ( fun bookList -> bookList.BookInfo = bookType ) data.Value.BookList
        for book in data.Value.BookList.[index].BookList do
            let mutable title = sprintf "%s - (%s)" book.BookTitle book.BookPublishDate
            if String.IsNullOrEmpty book.BooksUrl then
                title <- title + " (无下载资源)"
            subMenu.Add ( title, Action ( fun () ->
                      let trimedTitle = book.BookTitle.TrimEnd()
                      downloadQueue.Add ( book.BooksUrl, Path.GetExtension( book.BooksUrl ) |> sprintf "%s_%s%s" trimedTitle book.BookPublishDate )
                      downloadQueue.StartAsync ()
//                      Console.ReadKey ()
                      printfn "添加资源到下载队列: %A" book.BooksUrl
                      subMenu.Display () |> ignore ) )
            |> ignore
        
        if program.NavigationEnabled && not (subMenu.Contains("Go back")) then
            subMenu.Add("Go back", Action (fun () -> this.Menu.Display () |> ignore)) |> ignore
        
        Output.WriteLine ( ConsoleColor.Cyan, sprintf "选择了分类: %s 最近更新日期: %s 地址: %s" bookType.BookTypeName bookType.LatestUpdateDate bookType.TheTypeAllBooksUrl )
        
        if data.Value.BookList.[index].BookList.Length = 0 then
            Output.WriteLine ( ConsoleColor.Red, "当前分类下无可用资源, 任意键返回" )
            Console.ReadKey () |> ignore
            this.Menu.Display ()
        else
            subMenu.Display ()
    
    member val Menu : Menu = null with get, set

    override this.Display () =
        
        this.Menu <- Menu ()
        
        for i in 0 .. data.Value.BookList.Length - 1 do
            this.Menu.Add ( sprintf "%s (%s更新)" data.Value.BookList.[i].BookInfo.BookTypeName data.Value.BookList.[i].BookInfo.LatestUpdateDate, Action ( fun () ->
                      selectBookType data.Value.BookList.[i].BookInfo |> ignore ) ) |> ignore
        
        if program.NavigationEnabled && not (this.Menu.Contains("Go back")) then
            this.Menu.Add("Go back", Action (fun () -> program.NavigateBack() |> ignore)) |> ignore
        
        this.Menu.Display ()

        base.Display ()


type MainLoop =
    inherit Program
    
    new () as this =
        {
           inherit Program ( "MingHuiToolkit", true )
        }
        then
            this.AddPage( MainPage( this ) )
            this.AddPage( MingHuiCacheDataPage( this ) )
            this.AddPage( MingHuiDownloadPage( this ) )
            this.AddPage( MingHuiDownloadQueuePage( this ) )
            this.AddPage( BookTypeSelectPage( this ) )
            this.SetPage<MainPage>() |> ignore
            this.Run()


[<EntryPoint>]
let main argv =
    
    if File.Exists FILE_MINGHUI_CACHE then
        data <- Some ( Json.deserialize< MingHuiCacheData >( File.ReadAllText FILE_MINGHUI_CACHE ) )
        
        if data.IsSome then
            
            Output.WriteLine ( ConsoleColor.Green, "已加载缓存" )
    
    MainLoop ()
    
    0 // return an integer exit code
