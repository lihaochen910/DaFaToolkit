module MingHuiQiKanTool

open System
open System.Collections.Generic
open AngleSharp
open AngleSharp.Html.Dom


/// 明慧期刊枚举
type MingHuiQiKanEnum =
    | QiKan = 1    // 明慧期刊
    | TeKan = 2    // 明慧特刊


/// 书类型信息
type MingHuiQiKanBookTypeInfo = { BookTypeName : string; TheTypeAllBooksUrl : string; LatestUpdateDate : string; LatestUpdateDateTime : DateTime }


/// 书籍信息
type MingHuiQiKanInfo = { BookTitle : string; BooksUrl : string; BookPublishDate : string; BookPublishDateTime : DateTime }


/// 某分类书下的列表
type MingHuiTheBookList = { BookInfo : MingHuiQiKanBookTypeInfo; BookList : MingHuiQiKanInfo[] }


/// 持久化数据
type MingHuiCacheData = { BookList : list<MingHuiTheBookList>; CacheTimeStamp : DateTime }


let FILE_MINGHUI_CACHE = "MinghuiQikan.json"


/// 明慧期刊类型列表页面
let DIV_QIKAN_TYPE_LIST_MAXPAGE = "maxpage"
let DIV_QIKAN_TYPE_LIST_CURRENTPAGE = "currentpage"
let DIV_QIKAN_TYPE_LIST_BOOKSHELF = "qikan_listing0"
let DIV_QIKAN_TYPE_LIST_BOOKDATE = "annotation_over_cover_image"
let DIV_QIKAN_TYPE_LIST_BOOKTITLE = "annotation_over_cover_image_text"

/// 明慧期刊某期页面
let DIV_QIKAN_BOOKTITLE = "title"
let SPAN_QIKAN_BOOKTITLE = "lblTitle"
let DIV_QIKAN_BOOKURL = "htmlcontent"
let SPAN_QIKAN_BOOKURL = "lblHtmlCode"
let DIV_QIKAN_BOOKPUBDATE = "publish_date"
let SPAN_QIKAN_BOOKPUBDATE = "lblPublishDate"
let KEY_PDF = "PDF"
let KEY_PRINT = "打印版"

let MINGHUI_QIKAN_DOMAIN = "http://qikan.minghui.org"
let PARAM_CategoryID = "category_id"
let PARAM_LeiBieID = "leibie_id"
let PARAM_Page = "page"


/// 从url获取期刊列表
let fetchBookTypeList (context : IBrowsingContext) =
    async {
        
        let bookTypeList = List<MingHuiQiKanBookTypeInfo>()
        
        let url = sprintf "%s/display.aspx?%s=%d&%s=%d" MINGHUI_QIKAN_DOMAIN PARAM_CategoryID 1 PARAM_LeiBieID (int MingHuiQiKanEnum.QiKan)
        
        printfn "正在获取明慧期刊列表: %A" url
        
        try
            let! queryDocument = context.OpenAsync url |> Async.AwaitTask

            if queryDocument <> null then
                
                let div = queryDocument.QuerySelector( "div." + DIV_QIKAN_TYPE_LIST_BOOKSHELF ) :?> IHtmlDivElement
        
                let ulList = div.FirstElementChild :?> IHtmlUnorderedListElement
                
                for ulSubNode in ulList.Children do
                    let liNode = ulSubNode :?> IHtmlListItemElement
                    let aNode = liNode.FirstElementChild :?> IHtmlAnchorElement
                    let bookDate = (( aNode.GetElementsByClassName DIV_QIKAN_TYPE_LIST_BOOKDATE ).[0] :?> IHtmlDivElement ).TextContent
                    let bookTitle = (( aNode.GetElementsByClassName DIV_QIKAN_TYPE_LIST_BOOKTITLE ).[0] :?> IHtmlDivElement ).TextContent
                    let bookRelUrl = aNode.Href
                    
                    bookTypeList.Add { BookTypeName = bookTitle; TheTypeAllBooksUrl = bookRelUrl; LatestUpdateDate = bookDate; LatestUpdateDateTime = DateTime.Parse bookDate }
            
        with error -> printfn "获取明慧期刊列表时出现错误: %A\n%A" error.Message error.StackTrace

        // print
        for bookInfo in bookTypeList do
            printfn "%A %A %A" bookInfo.BookTypeName bookInfo.LatestUpdateDate bookInfo.TheTypeAllBooksUrl
        
        return bookTypeList.ToArray()
    }


let fetchTheTypeAllBooks (bookTypeInfo : MingHuiQiKanBookTypeInfo) (context : IBrowsingContext) =
    async {
        
        let qikanList = List<MingHuiQiKanInfo>()
        
        let mutable url = bookTypeInfo.TheTypeAllBooksUrl
        
        printfn "正在获取<%s>列表: %A" bookTypeInfo.BookTypeName url
        
        try
        
            let! queryDocument = context.OpenAsync url |> Async.AwaitTask
            
            if queryDocument <> null then
            
                let totalPage = (int ( queryDocument.QuerySelector( "div." + DIV_QIKAN_TYPE_LIST_MAXPAGE ) :?> IHtmlDivElement ).TextContent )
            
                for currentPage = 1 to totalPage do
                    
                    url <- bookTypeInfo.TheTypeAllBooksUrl + sprintf "&%s=%d" PARAM_Page currentPage
                    
                    printfn "正在获取<%s>列表第%d页(共%d页): %A" bookTypeInfo.BookTypeName currentPage totalPage url
                    
                    let! doc = context.OpenAsync url |> Async.AwaitTask

                    let div = doc.QuerySelector( "div." + DIV_QIKAN_TYPE_LIST_BOOKSHELF ) :?> IHtmlDivElement
                
                    let ulList = div.FirstElementChild :?> IHtmlUnorderedListElement
                    
                    for ulSubNode in ulList.Children do
                        let liNode = ulSubNode :?> IHtmlListItemElement
                        let aNode = liNode.FirstElementChild :?> IHtmlAnchorElement
                        let bookDate = (( aNode.GetElementsByClassName DIV_QIKAN_TYPE_LIST_BOOKDATE ).[0] :?> IHtmlDivElement ).TextContent
                        let bookTitle = (( aNode.GetElementsByClassName DIV_QIKAN_TYPE_LIST_BOOKTITLE ).[0] :?> IHtmlDivElement ).TextContent
                        let bookRelUrl = aNode.Href
                        let mutable bookDownloadUrl : string = null
                        
                        // 需要跳转到书页面获取下载地址
                        let! bookDocument = context.OpenAsync bookRelUrl |> Async.AwaitTask
                        let bookSpan = bookDocument.GetElementById SPAN_QIKAN_BOOKURL :?> IHtmlSpanElement
                        let chilrenArray = [| for child in bookSpan.Children -> child |]
                        let mutable pdfIndex = -1
                        
                        // 关键字筛选
                        try
                            chilrenArray
                            |> Array.tryFindIndex ( fun elm ->
                                if elm :? IHtmlAnchorElement then
                                    elm.TextContent.ToUpper().Contains KEY_PDF
                                    || elm.TextContent.Contains KEY_PRINT
                                    || (elm :?> IHtmlAnchorElement).Href.ToUpper().Contains KEY_PDF
                                else
                                    false )
                            |> (fun i -> pdfIndex <- if i.IsSome then i.Value else -1)
                        with error -> printfn "%s\n%s" error.Message error.StackTrace
                        
                        if pdfIndex <> -1 then
                            bookDownloadUrl <- ( chilrenArray.[pdfIndex] :?> IHtmlAnchorElement ).Href
                            printfn "找到<%s> %s 打印版下载地址: %s" bookTitle bookDate bookDownloadUrl
                        else
                            printfn "未找到<%s> %s 打印版下载地址" bookTitle bookDate
                            
                        qikanList.Add { BookTitle = bookTitle; BooksUrl = bookDownloadUrl; BookPublishDate = bookDate; BookPublishDateTime = DateTime.Parse bookDate }
                        

        with error -> printfn "获取<%s>列表时出现错误: %A\n%A" bookTypeInfo.BookTypeName error.Message error.StackTrace
        
        qikanList.Sort (fun a b -> b.BookPublishDateTime.CompareTo a.BookPublishDateTime ) 
        
        return qikanList.ToArray()
    }


/// 获取最新数据
let fetchLatestData =
    
    printfn "开始获取最新数据"
    
    let config = Configuration.Default.WithDefaultLoader()

    let context = BrowsingContext.New config
    
    let bookDictionary = Dictionary<MingHuiQiKanBookTypeInfo, MingHuiQiKanInfo[]>()
    
    // 首先获取大类列表
    fetchBookTypeList context
    |> Async.RunSynchronously
    |> Array.iter
           // 接着根据大类列表获取子类列表
           (fun bookInfo ->
                fetchTheTypeAllBooks bookInfo context
                |> Async.RunSynchronously
                |> (fun bookArray ->
                        bookDictionary.Add( bookInfo, bookArray )
                        printfn "已缓存类别列表:%s" bookInfo.BookTypeName ) )
    
    // debug print
//    for kvp in bookDictionary do
//        printfn "===== %s =====" kvp.Key.BookTypeName
//        for bookInfo in kvp.Value do
//            printfn "%A %A %A" bookInfo.BookTitle bookInfo.BookPublishDate bookInfo.BooksUrl
//        printfn "=============="
    
    let mutable bookList : list<MingHuiTheBookList> = []
    
    for kvp in bookDictionary do
        bookList <- List.append bookList [ { BookInfo = kvp.Key; BookList = kvp.Value } ] 
    
    { BookList = bookList; CacheTimeStamp = DateTime.Now }
