# PpsEnvironment

Base class for application data. Holds information about view
classes, exception, connection, synchronisation and the script
engine.

### Declaration

`public class PpsEnvironment()`




# GetProxyRequest

Get a proxy request for the request path.

### Declaration

`public GetProxyRequest(System.String path,System.String displayName,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *path* |  |
| System.String | *displayName* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsProxyRequest |  |




# GetProxyRequest

Get a proxy request for the request path.

### Declaration

`public GetProxyRequest(System.Uri uri,System.String displayName,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Uri | *uri* |  |
| System.String | *displayName* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsProxyRequest |  |




# TryGetOfflineObject

Loads an item from offline cache.

### Declaration

`protected virtual TryGetOfflineObject(System.Net.WebRequest request,TecWare.PPSn.IPpsProxyTask task,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Net.WebRequest | *request* | Selects the item. |
| TecWare.PPSn.IPpsProxyTask | *task* | Out: the Task returning the item. |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Boolean | True if successfull. |




# GetViewData



### Declaration

`public virtual GetViewData(TecWare.PPSn.PpsShellGetList arguments,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.PPSn.PpsShellGetList | *arguments* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Collections.Generic.IEnumerable{TecWare.DE.Data.IDataRow} |  |




# GetRemoteViewData



### Declaration

`protected GetRemoteViewData(TecWare.PPSn.PpsShellGetList arguments,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.PPSn.PpsShellGetList | *arguments* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Collections.Generic.IEnumerable{TecWare.DE.Data.IDataRow} |  |




# OnBeforeSynchronization

Gets called before a synchronization run.

### Declaration

`protected virtual OnBeforeSynchronization()`




# OnAfterSynchronization

Gets called after a synchronization run.

### Declaration

`protected virtual OnAfterSynchronization()`




# OnSystemOnlineAsync

Is called when the system changes the state to online.

### Declaration

`protected virtual OnSystemOnlineAsync()`

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task |  |




# OnSystemOfflineAsync

Is called when the system changes the state to offline.

### Declaration

`protected virtual OnSystemOfflineAsync()`

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task |  |




# OnMasterDataTableChanged

Is called when a table gets changed.

### Declaration

`public virtual OnMasterDataTableChanged(TecWare.PPSn.PpsDataTableOperationEventArgs args,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.PPSn.PpsDataTableOperationEventArgs | *args* |  |




# ForceOnlineAsync

Enforce online mode and return true, if the operation was successfull.

### Declaration

`public ForceOnlineAsync(System.Boolean throwException,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Boolean | *throwException* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{System.Boolean} |  |




# Request



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public get Request()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.DE.Networking.BaseWebRequest |  |




# Encoding

Default encodig for strings.

### Declaration

`public get Encoding()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Text.Encoding |  |




# BaseUri

Internal Uri of the environment.

### Declaration

`public get BaseUri()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Uri |  |




# WebProxy



### Declaration

`public get WebProxy()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsWebProxy |  |




# StatusOfProxy



### Declaration

`public get StatusOfProxy()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.ProxyStatus |  |




# LocalConnection

Connection to the local datastore

### Declaration

`System.ObsoleteAttribute.#ctor(System.String)`
`public get LocalConnection()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Data.SQLite.SQLiteConnection |  |




# MasterData

Access to the local store for the synced data.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public get MasterData()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsMasterData |  |




# CompileAsync

Compiles a chunk in the background.

### Declaration

`public CompileAsync(System.IO.TextReader tr,System.String sourceLocation,System.Boolean throwException,System.Collections.Generic.KeyValuePair{System.String,System.Type}[] arguments,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.IO.TextReader | *tr* | Chunk source. |
| System.String | *sourceLocation* | Source location for the debug information. |
| System.Boolean | *throwException* | If the compile fails, should be raised a exception. |
| System.Collections.Generic.KeyValuePair{System.String,System.Type}[] | *arguments* | Argument definition for the chunk. |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{Neo.IronLua.LuaChunk} | Compiled chunk |




# CompileLambdaAsync``1



### Declaration

`public CompileLambdaAsync``1(System.String code,System.String sourceLocation,System.Boolean throwException,System.String[] argumentNames,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *code* |  |
| System.String | *sourceLocation* | Source location for the debug information. |
| System.Boolean | *throwException* | If the compile fails, should be raised a exception. |
| System.String[] | *argumentNames* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{{T}} |  |




# CompileLambdaAsync``1



### Declaration

`public CompileLambdaAsync``1(System.Xml.Linq.XElement xSource,System.Boolean throwException,System.String[] argumentNames,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Xml.Linq.XElement | *xSource* |  |
| System.Boolean | *throwException* |  |
| System.String[] | *argumentNames* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{{T}} |  |




# CompileLambdaAsync``1



### Declaration

`public CompileLambdaAsync``1(System.Xml.XmlReader xml,System.Boolean throwException,System.String[] argumentNames,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Xml.XmlReader | *xml* |  |
| System.Boolean | *throwException* |  |
| System.String[] | *argumentNames* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{{T}} |  |




# CompileAsync

Compiles a chunk in the background.

### Declaration

`public CompileAsync(System.String sourceCode,System.String sourceFileName,System.Boolean throwException,System.Collections.Generic.KeyValuePair{System.String,System.Type}[] arguments,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *sourceCode* | Source code of the chunk. |
| System.String | *sourceFileName* | Source location for the debug information. |
| System.Boolean | *throwException* | If the compile fails, should be raised a exception. |
| System.Collections.Generic.KeyValuePair{System.String,System.Type}[] | *arguments* | Argument definition for the chunk. |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{Neo.IronLua.LuaChunk} | Compiled chunk |




# CompileAsync

Compiles a chunk in the background.

### Declaration

`public CompileAsync(System.Xml.Linq.XElement xSource,System.Boolean throwException,System.Collections.Generic.KeyValuePair{System.String,System.Type}[] arguments,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Xml.Linq.XElement | *xSource* | Source element of the chunk. The Value is the source code, and the positions encoded in the tag (see GetXmlPositionFromAttributes). |
| System.Boolean | *throwException* | If the compile fails, should be raised a exception. |
| System.Collections.Generic.KeyValuePair{System.String,System.Type}[] | *arguments* | Argument definition for the chunk. |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{Neo.IronLua.LuaChunk} | Compiled chunk |




# CompileAsync

Load an compile the file from a remote source.

### Declaration

`public CompileAsync(TecWare.DE.Networking.BaseWebRequest request,System.Uri source,System.Boolean throwException,System.Collections.Generic.KeyValuePair{System.String,System.Type}[] arguments,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.DE.Networking.BaseWebRequest | *request* |  |
| System.Uri | *source* | Source uri |
| System.Boolean | *throwException* | Throw an exception on fail |
| System.Collections.Generic.KeyValuePair{System.String,System.Type}[] | *arguments* | Argument definition for the chunk. |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{Neo.IronLua.LuaChunk} | Compiled chunk |




# CreateChunk

Compile the chunk in a background thread and hold the UI thread

### Declaration

`public CreateChunk(System.Xml.Linq.XElement xCode,System.Boolean throwException,System.Collections.Generic.KeyValuePair{System.String,System.Type}[] arguments,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Xml.Linq.XElement | *xCode* |  |
| System.Boolean | *throwException* |  |
| System.Collections.Generic.KeyValuePair{System.String,System.Type}[] | *arguments* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| Neo.IronLua.LuaChunk |  |




# RunScript

Executes the script (the script is always execute in the UI thread).

### Declaration

`public RunScript(Neo.IronLua.LuaChunk chunk,Neo.IronLua.LuaTable env,System.Boolean throwException,System.Object[] arguments,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| Neo.IronLua.LuaChunk | *chunk* |  |
| Neo.IronLua.LuaTable | *env* |  |
| System.Boolean | *throwException* |  |
| System.Object[] | *arguments* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| Neo.IronLua.LuaResult |  |




# RunScriptWithReturn``1

Executes the script, and returns a value (the script is always execute in the UI thread).

### Declaration

`public RunScriptWithReturn``1(Neo.IronLua.LuaChunk chunk,Neo.IronLua.LuaTable env,System.Nullable{{T}} returnOnException,System.Object[] arguments,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| Neo.IronLua.LuaChunk | *chunk* |  |
| Neo.IronLua.LuaTable | *env* |  |
| System.Nullable{{T}} | *returnOnException* |  |
| System.Object[] | *arguments* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| {T} |  |




# RunAsync

Creates a new execution thread for the function in the background.

### Declaration

`public RunAsync(System.Func{System.Threading.Tasks.Task} task,System.String name,System.Threading.CancellationToken cancellationToken,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Func{System.Threading.Tasks.Task} | *task* | Action to run. |
| System.String | *name* | name of the background thread |
| System.Threading.CancellationToken | *cancellationToken* | cancellation option |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task |  |




# RunAsync



### Declaration

`public RunAsync(System.Func{System.Threading.Tasks.Task} task,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Func{System.Threading.Tasks.Task} | *task* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task |  |




# RunAsync``1

Creates a new execution thread for the function in the background.

### Declaration

`public RunAsync``1(System.Func{System.Threading.Tasks.Task{{T}}} task,System.String name,System.Threading.CancellationToken cancellationToken,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Func{System.Threading.Tasks.Task{{T}}} | *task* |  |
| System.String | *name* |  |
| System.Threading.CancellationToken | *cancellationToken* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{{T}} |  |




# RunAsync``1



### Declaration

`public RunAsync``1(System.Func{System.Threading.Tasks.Task{{T}}} task,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Func{System.Threading.Tasks.Task{{T}}} | *task* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{{T}} |  |




# HandleDataRemoveException

Show a exception for a remove operation.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public HandleDataRemoveException(System.Exception e,System.Object objectName,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Exception | *e* |  |
| System.Object | *objectName* |  |




# LoadPaneDataAsync



### Declaration

`public LoadPaneDataAsync(TecWare.DE.Networking.BaseWebRequest request,Neo.IronLua.LuaTable arguments,System.Uri paneUri,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.DE.Networking.BaseWebRequest | *request* |  |
| Neo.IronLua.LuaTable | *arguments* |  |
| System.Uri | *paneUri* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{System.Object} |  |




# run ( LuaRunBackground )

Executes the function or task in an background thread.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.String,System.Boolean)`
`public run(System.Object func,System.Object[] args,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Object | *func* |  |
| System.Object[] | *args* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsLuaTask |  |




# async ( LuaAsync )

Executes the function or task, async in the ui thread.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.String,System.Boolean)`
`public async(System.Object func,System.Object[] args,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Object | *func* |  |
| System.Object[] | *args* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsLuaTask |  |




# runSync ( RunSynchronization )



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.String,System.Boolean)`
`public runSync()`

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task |  |




# createTransaction ( CreateTransaction )



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.String,System.Boolean)`
`public createTransaction()`

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsMasterDataTransaction |  |




# getServerRowValue ( GetServerRowValue )



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.String,System.Boolean)`
`public getServerRowValue(System.Object v,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Object | *v* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Object |  |




# GetVisualParent



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public GetVisualParent(System.Windows.DependencyObject currentControl,System.Object control,System.Boolean throwException,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Windows.DependencyObject | *currentControl* |  |
| System.Object | *control* |  |
| System.Boolean | *throwException* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Windows.DependencyObject |  |




# GetLogicalParent



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public GetLogicalParent(System.Windows.DependencyObject currentControl,System.Object control,System.Boolean throwException,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Windows.DependencyObject | *currentControl* |  |
| System.Object | *control* |  |
| System.Boolean | *throwException* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Windows.DependencyObject |  |




# GetResource

Find the resource.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public GetResource(System.Object key,System.Windows.DependencyObject dependencyObject,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Object | *key* |  |
| System.Windows.DependencyObject | *dependencyObject* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Object |  |




# GetLocalTempFileInfo

Create a local tempfile name for this objekt

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public GetLocalTempFileInfo(TecWare.PPSn.PpsObject obj,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.PPSn.PpsObject | *obj* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.IO.FileInfo |  |




# CreateDataRowFilter



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public CreateDataRowFilter(System.String filterExpr,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *filterExpr* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Predicate{TecWare.DE.Data.IDataRow} |  |




# PullRevisionAsync



### Declaration

`System.ObsoleteAttribute.#ctor(System.String)`
`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public PullRevisionAsync(TecWare.PPSn.PpsObject obj,System.Int64 revId,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.PPSn.PpsObject | *obj* |  |
| System.Int64 | *revId* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{TecWare.PPSn.PpsObjectDataSet} |  |




# RunServerReportAsync



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public RunServerReportAsync(System.String reportName,Neo.IronLua.LuaTable arguments,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *reportName* |  |
| Neo.IronLua.LuaTable | *arguments* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{System.String} |  |




# BlockAllUI



### Declaration

`public BlockAllUI(System.Windows.Threading.DispatcherFrame frame,System.String message,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Windows.Threading.DispatcherFrame | *frame* |  |
| System.String | *message* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.IDisposable |  |




# OnIndex



### Declaration

`protected override OnIndex(System.Object key,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Object | *key* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Object |  |




# UI ( LuaUI )

Lua ui-wpf framwework.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.String,System.Boolean)`
`public get UI()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.UI.LuaUI |  |




# FieldFactory

Field factory for controls

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public get FieldFactory()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| Neo.IronLua.LuaTable |  |




# Lua

Assess lua

### Declaration

`public get Lua()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| Neo.IronLua.Lua |  |




# AttachmentObjectTyp

Object typ for blob data.

### Declaration

`public static AttachmentObjectTyp()`

### Returns

| **Type** | **Description** |
| --- | --- |
| System.String |  |




# CreateNewObjectAsync

Create a new object in the local database.

### Declaration

`public CreateNewObjectAsync(TecWare.PPSn.PpsObjectInfo objectInfo,System.String mimeType,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.PPSn.PpsObjectInfo | *objectInfo* |  |
| System.String | *mimeType* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{TecWare.PPSn.PpsObject} |  |




# CreateNewObjectFromFileAsync



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public CreateNewObjectFromFileAsync(System.String fileName,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *fileName* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{TecWare.PPSn.PpsObject} |  |




# CreateNewObjectFromStreamAsync



### Declaration

`public CreateNewObjectFromStreamAsync(System.IO.Stream dataSource,System.String name,System.String mimeType,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.IO.Stream | *dataSource* |  |
| System.String | *name* |  |
| System.String | *mimeType* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{TecWare.PPSn.PpsObject} |  |




# CreateNewObjectAsync

Create a new object.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public CreateNewObjectAsync(System.Guid guid,System.String typ,System.String nr,System.Boolean isRev,System.String mimeType,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Guid | *guid* |  |
| System.String | *typ* |  |
| System.String | *nr* |  |
| System.Boolean | *isRev* |  |
| System.String | *mimeType* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{TecWare.PPSn.PpsObject} |  |




# PushObjectAsync

Call push function of an object.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public PushObjectAsync(TecWare.PPSn.PpsObject obj,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.PPSn.PpsObject | *obj* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task |  |




# GetObjectInfoSyncObject

Get object info list synchronization object.

### Declaration

`protected GetObjectInfoSyncObject()`

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Object |  |




# GetRemoveListObjectInfo

Remove list for object infos

### Declaration

`protected GetRemoveListObjectInfo()`

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Collections.Generic.List{System.String} |  |




# UpdateObjectInfo

Update object info structur.

### Declaration

`protected UpdateObjectInfo(System.Xml.Linq.XElement x,System.Collections.Generic.List{System.String} removeObjectInfo,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Xml.Linq.XElement | *x* |  |
| System.Collections.Generic.List{System.String} | *removeObjectInfo* |  |




# ClearObjectInfo

Remove object infos.

### Declaration

`protected ClearObjectInfo(System.Collections.Generic.List{System.String} removeObjectInfo,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Collections.Generic.List{System.String} | *removeObjectInfo* |  |




# RegisterObjectInfoSchema



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public RegisterObjectInfoSchema(System.String name,Neo.IronLua.LuaTable arguments,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *name* |  |
| Neo.IronLua.LuaTable | *arguments* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsObjectInfo |  |




# GetDocumentUri

Get the uri for a document definition.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public GetDocumentUri(System.String schema,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *schema* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.String |  |




# GetDocumentDefinitionAsync



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public GetDocumentDefinitionAsync(System.String schema,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *schema* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{TecWare.PPSn.Data.PpsDataSetDefinitionDesktop} |  |




# GetObject



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public GetObject(System.Int64 localId,System.Boolean throwException,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Int64 | *localId* |  |
| System.Boolean | *throwException* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsObject |  |




# GetObject



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public GetObject(System.Guid guid,System.Boolean throwException,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Guid | *guid* |  |
| System.Boolean | *throwException* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsObject |  |




# GetObjectInfo



### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public GetObjectInfo(System.String objectTyp,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *objectTyp* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| Neo.IronLua.LuaTable |  |




# ActiveObjectData

Active objects data.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public get ActiveObjectData()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.IPpsActiveObjectDataTable |  |




# ObjectInfos

Object info structure.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public get ObjectInfos()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsEnvironmentCollection{TecWare.PPSn.PpsObjectInfo} |  |




# EnvironmentService

Resource key for the environment

### Declaration

`public const EnvironmentService()`

### Returns

| **Type** | **Description** |
| --- | --- |
| System.String |  |




# WindowPaneService

Resource key for the window pane.

### Declaration

`public const WindowPaneService()`

### Returns

| **Type** | **Description** |
| --- | --- |
| System.String |  |




# AddIdleAction ( AddIdleAction )

Add a idle action.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.String,System.Boolean)`
`public AddIdleAction(System.Func{System.Int32,System.Boolean} onIdle,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Func{System.Int32,System.Boolean} | *onIdle* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.IPpsIdleAction |  |




# AddIdleAction

Add a idle action.

### Declaration

`public AddIdleAction(TecWare.PPSn.IPpsIdleAction idleAction,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.PPSn.IPpsIdleAction | *idleAction* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.IPpsIdleAction |  |




# RemoveIdleAction ( RemoveIdleAction )

Remove a idle action.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.String,System.Boolean)`
`public RemoveIdleAction(TecWare.PPSn.IPpsIdleAction idleAction,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.PPSn.IPpsIdleAction | *idleAction* |  |




# DefaultExecutedHandler



### Declaration

`public get DefaultExecutedHandler()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Windows.Input.ExecutedRoutedEventHandler |  |




# DefaultCanExecuteHandler



### Declaration

`public get DefaultCanExecuteHandler()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Windows.Input.CanExecuteRoutedEventHandler |  |




# TecWare#PPSn#IPpsShell#BeginInvoke



### Declaration

`TecWare#PPSn#IPpsShell#BeginInvoke(System.Action action,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Action | *action* |  |




# TecWare#PPSn#IPpsShell#InvokeAsync



### Declaration

`TecWare#PPSn#IPpsShell#InvokeAsync(System.Action action,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Action | *action* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task |  |




# TecWare#PPSn#IPpsShell#InvokeAsync``1



### Declaration

`TecWare#PPSn#IPpsShell#InvokeAsync``1(System.Func{{T}} func,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Func{{T}} | *func* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{{T}} |  |




# AppendException ( AppendException )

Append a exception to the log.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.String,System.Boolean)`
`public AppendException(System.Exception exception,System.String alternativeMessage,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Exception | *exception* |  |
| System.String | *alternativeMessage* |  |




# ShowException ( ShowException )

Display the exception dialog.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.String,System.Boolean)`
`public ShowException(System.Exception exception,System.String alternativeMessage,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Exception | *exception* |  |
| System.String | *alternativeMessage* |  |




# ShowExceptionAsync ( ShowExceptionAsync )

Display the exception dialog in the main ui-thread.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.String,System.Boolean)`
`public ShowExceptionAsync(System.Exception exception,System.String alternativeMessage,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Exception | *exception* |  |
| System.String | *alternativeMessage* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task |  |




# ShowException

Display the exception dialog.

### Declaration

`public ShowException(TecWare.PPSn.ExceptionShowFlags flags,System.Exception exception,System.String alternativeMessage,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.PPSn.ExceptionShowFlags | *flags* |  |
| System.Exception | *exception* |  |
| System.String | *alternativeMessage* |  |




# ShowExceptionAsync

Display the exception dialog in the main ui-thread.

### Declaration

`public ShowExceptionAsync(TecWare.PPSn.ExceptionShowFlags flags,System.Exception exception,System.String alternativeMessage,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.PPSn.ExceptionShowFlags | *flags* |  |
| System.Exception | *exception* |  |
| System.String | *alternativeMessage* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task |  |




# ShowExceptionDialog

Display the exception dialog.

### Declaration

`public ShowExceptionDialog(System.Windows.Window dialogOwner,TecWare.PPSn.ExceptionShowFlags flags,System.Exception exception,System.String alternativeMessage,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Windows.Window | *dialogOwner* |  |
| TecWare.PPSn.ExceptionShowFlags | *flags* |  |
| System.Exception | *exception* |  |
| System.String | *alternativeMessage* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Boolean |  |




# ShowTrace



### Declaration

`public ShowTrace(System.Windows.Window owner,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Windows.Window | *owner* |  |




# TracePaneType

Returns the pane declaration for the trace pane.

### Declaration

`public virtual get TracePaneType()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Type |  |




# MsgBox

Display a simple messagebox

### Declaration

`public MsgBox(System.String text,System.Windows.MessageBoxButton button,System.Windows.MessageBoxImage image,System.Windows.MessageBoxResult defaultResult,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *text* |  |
| System.Windows.MessageBoxButton | *button* |  |
| System.Windows.MessageBoxImage | *image* |  |
| System.Windows.MessageBoxResult | *defaultResult* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Windows.MessageBoxResult |  |




# MsgBoxAsync

Display a simple messagebox in the main ui-thread.

### Declaration

`public MsgBoxAsync(System.String text,System.Windows.MessageBoxButton button,System.Windows.MessageBoxImage image,System.Windows.MessageBoxResult defaultResult,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *text* |  |
| System.Windows.MessageBoxButton | *button* |  |
| System.Windows.MessageBoxImage | *image* |  |
| System.Windows.MessageBoxResult | *defaultResult* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{System.Windows.MessageBoxResult} |  |




# TecWare#PPSn#IPpsShell#ShowMessageAsync



### Declaration

`TecWare#PPSn#IPpsShell#ShowMessageAsync(System.String message,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *message* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task |  |




# FindResource``1

Find a global resource.

### Declaration

`public FindResource``1(System.Object resourceKey,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Object | *resourceKey* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| {T} |  |




# TecWare#PPSn#UI#IPpsXamlCode#CompileCode



### Declaration

`TecWare#PPSn#UI#IPpsXamlCode#CompileCode(System.Uri uri,System.String code,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Uri | *uri* |  |
| System.String | *code* |  |




# #ctor



### Declaration

`public #ctor(TecWare.PPSn.PpsEnvironmentInfo info,System.Net.NetworkCredential userInfo,System.Windows.ResourceDictionary mainResources,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| TecWare.PPSn.PpsEnvironmentInfo | *info* |  |
| System.Net.NetworkCredential | *userInfo* |  |
| System.Windows.ResourceDictionary | *mainResources* |  |




# InitAsync

Initialize environmnet.

### Declaration

`public InitAsync(System.IProgress{System.String} progress,System.Boolean bootOffline,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.IProgress{System.String} | *progress* |  |
| System.Boolean | *bootOffline* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.Tasks.Task{TecWare.PPSn.PpsEnvironmentModeResult} |  |




# Dispose

Destroy environment.

### Declaration

`public Dispose()`




# Dispose



### Declaration

`protected virtual Dispose(System.Boolean disposing,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Boolean | *disposing* |  |




# IsNetworkPresent

Test if Network is present.

### Declaration

`public get IsNetworkPresent()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Boolean |  |




# RegisterService

Register Service to the environment root.

### Declaration

`public RegisterService(System.String key,System.Object service,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.String | *key* |  |
| System.Object | *service* |  |




# GetService



### Declaration

`public GetService(System.Type serviceType,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Type | *serviceType* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Object |  |




# TecWare#PPSn#IPpsShell#Await



### Declaration

`TecWare#PPSn#IPpsShell#Await(System.Threading.Tasks.Task task,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Threading.Tasks.Task | *task* |  |




# TecWare#PPSn#IPpsShell#Await``1



### Declaration

`TecWare#PPSn#IPpsShell#Await``1(System.Threading.Tasks.Task{{T}} task,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Threading.Tasks.Task{{T}} | *task* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| {T} |  |




# EnvironmentId

Internal Id of the environment.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public get EnvironmentId()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Int32 |  |




# UserId

User Id, not the contact id.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public get UserId()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Int64 |  |




# UsernameDisplay

Displayname for UI.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public get UsernameDisplay()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.String |  |




# CurrentMode

The current mode of the environment.

### Declaration

`public get CurrentMode()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsEnvironmentMode |  |




# CurrentState

The current state of the environment.

### Declaration

`public get CurrentState()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsEnvironmentState |  |




# IsOnline

Current state of the environment

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public get IsOnline()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Boolean |  |




# DataListItemTypes

Data list items definitions

### Declaration

`public get DataListItemTypes()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsEnvironmentCollection{TecWare.PPSn.Data.PpsDataListItemDefinition} |  |




# DataListTemplateSelector

Basic template selector for the item selector

### Declaration

`public get DataListTemplateSelector()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.Controls.PpsDataListTemplateSelector |  |




# Dispatcher

Dispatcher of the ui-thread.

### Declaration

`public get Dispatcher()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Windows.Threading.Dispatcher |  |




# TecWare#PPSn#IPpsShell#Context

Synchronisation

### Declaration

`get TecWare#PPSn#IPpsShell#Context()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Threading.SynchronizationContext |  |




# Traces

Access to the current collected informations.

### Declaration

`public get Traces()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsTraceLog |  |




# Statistics

Returns the available Statistics

### Declaration

`public get Statistics()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.Collections.Generic.List{TecWare.PPSn.PpsEnvironment.StatisticElement} |  |




# LocalPath

Path of the local data for the user.

### Declaration

`Neo.IronLua.LuaMemberAttribute.#ctor(System.Boolean)`
`public get LocalPath()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| System.IO.DirectoryInfo |  |




# TecWare#PPSn#IPpsShell#LuaLibrary



### Declaration

`get TecWare#PPSn#IPpsShell#LuaLibrary()`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |

### Returns

| **Type** | **Description** |
| --- | --- |
| Neo.IronLua.LuaTable |  |




# GetEnvironment

Get the environment, that is attached to the current ui-element.

### Declaration

`public static GetEnvironment(System.Windows.FrameworkElement ui,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Windows.FrameworkElement | *ui* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsEnvironment |  |




# GetEnvironment

Get the Environment, that is attached to the current application.

### Declaration

`public static GetEnvironment()`

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.PpsEnvironment |  |




# GetCurrentPane

Get the current pane from the ui element.

### Declaration

`public static GetCurrentPane(System.Windows.FrameworkElement ui,)`

### Parameters

| **Type** | **Name** | **Description** |
| --- | --- | --- |
| System.Windows.FrameworkElement | *ui* |  |

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.UI.IPpsWindowPane |  |




# GetCurrentPane

Get the current pane from the focused element.

### Declaration

`public static GetCurrentPane()`

### Returns

| **Type** | **Description** |
| --- | --- |
| TecWare.PPSn.UI.IPpsWindowPane |  |
