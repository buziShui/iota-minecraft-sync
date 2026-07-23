Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading
Imports Newtonsoft.Json.Linq

Public Module ModIotaSync
    Public Const IotaLauncherVersion As String = "1.3.2"
    Private ReadOnly ConfigPath As String = Paths.Base & "PCL\IotaSync.json"
    Private ReadOnly HistoryPath As String = Paths.Base & "PCL\IotaSyncHistory.json"
    Private ReadOnly ConfigLock As New Object
    Private SyncCancellation As CancellationTokenSource
    Private LastFailedSource As JObject
    Private SyncTotalBytes As Long
    Private SyncCompletedBytes As Long
    Private SyncStartedAt As Date

    Public Event IotaSyncProgressChanged(status As String, progress As Double, canCancel As Boolean, failed As Boolean)

    Private Sub ReportSyncProgress(status As String, Optional failed As Boolean = False, Optional completed As Boolean = False)
        Dim progress = If(SyncTotalBytes <= 0, 0, Math.Min(1, SyncCompletedBytes / CDbl(SyncTotalBytes)))
        RaiseEvent IotaSyncProgressChanged(status, progress, Not completed AndAlso SyncCancellation IsNot Nothing AndAlso Not SyncCancellation.IsCancellationRequested, failed)
    End Sub

    Public Sub CancelIotaSync()
        If SyncCancellation IsNot Nothing Then SyncCancellation.Cancel()
    End Sub

    Public Function GetLastFailedSource() As JObject
        Return If(LastFailedSource Is Nothing, Nothing, DirectCast(LastFailedSource.DeepClone(), JObject))
    End Function

    Public Function LoadSources() As JArray
        SyncLock ConfigLock
            Try
                If File.Exists(ConfigPath) Then Return JArray.Parse(File.ReadAllText(ConfigPath))
            Catch ex As Exception
                Logger.Warn(ex, "读取 Iota 同步源失败")
            End Try
            Return New JArray()
        End SyncLock
    End Function

    Public Sub SaveSources(sources As JArray)
        SyncLock ConfigLock
            Directory.CreateDirectory(IO.Path.GetDirectoryName(ConfigPath))
            Dim temp = ConfigPath & ".new"
            File.WriteAllText(temp, sources.ToString(Newtonsoft.Json.Formatting.Indented))
            If File.Exists(ConfigPath) Then File.Replace(temp, ConfigPath, Nothing) Else File.Move(temp, ConfigPath)
        End SyncLock
    End Sub

    Public Function NormalizeSourceAddress(address As String) As String
        address = address.Trim()
        If Not address.Contains("://") Then address = "http://" & address
        Dim uri As Uri = Nothing
        If Not Uri.TryCreate(address, UriKind.Absolute, uri) OrElse (uri.Scheme <> Uri.UriSchemeHttp AndAlso uri.Scheme <> Uri.UriSchemeHttps) Then Throw New Exception("同步地址必须是有效的 HTTP 或 HTTPS 绝对地址")
        Return address.TrimEnd("/"c)
    End Function

    Public Sub RemoveSource(instanceId As String)
        Dim sources = LoadSources()
        For i = sources.Count - 1 To 0 Step -1
            If sources(i)("instanceId")?.ToString() = instanceId Then sources.RemoveAt(i)
        Next
        SaveSources(sources)
    End Sub

    Public Sub RemoveSource(address As String, instanceId As String)
        Dim sources = LoadSources()
        For i = sources.Count - 1 To 0 Step -1
            If String.Equals(sources(i)("address")?.ToString(), address, StringComparison.OrdinalIgnoreCase) AndAlso
               sources(i)("instanceId")?.ToString() = instanceId Then sources.RemoveAt(i)
        Next
        SaveSources(sources)
    End Sub

    Public Function GetSourceManifest(source As JObject) As JObject
        Return RequestJson(
            source("address").ToString(),
            source("code").ToString(),
            "manifest",
            CInt(source("instanceId")))
    End Function

    Public Sub SaveOptionalSelections(address As String, instanceId As String, selections As JObject)
        SaveSourceProperty(address, instanceId, "optional", selections)
    End Sub

    Public Sub SaveIgnoredSelections(address As String, instanceId As String, selections As JArray)
        SaveSourceProperty(address, instanceId, "ignored", selections)
    End Sub

    Public Sub BindSourcePath(address As String, instanceId As String, instancePath As String)
        instancePath = IO.Path.GetFullPath(instancePath.Trim()).TrimEnd("\"c) & "\"
        If Not Directory.Exists(instancePath) Then Throw New DirectoryNotFoundException("选择的实例目录不存在")
        SaveSourceProperty(address, instanceId, "instancePath", instancePath)
    End Sub

    Private Sub SaveSourceProperty(address As String, instanceId As String, propertyName As String, value As JToken)
        Dim sources = LoadSources()
        For Each source In sources.OfType(Of JObject)()
            If SourceMatches(source, address, instanceId) Then
                source(propertyName) = value.DeepClone()
                SaveSources(sources)
                Return
            End If
        Next
        Throw New Exception("同步源已不存在，请刷新列表")
    End Sub

    Private Function SourceMatches(source As JObject, address As String, instanceId As String) As Boolean
        Return String.Equals(source("address")?.ToString(), address, StringComparison.OrdinalIgnoreCase) AndAlso
               source("instanceId")?.ToString() = instanceId
    End Function

    Public Function ResolveInstancePath(source As JObject) As String
        Dim bound = source("instancePath")?.ToString()
        If Not String.IsNullOrWhiteSpace(bound) Then Return IO.Path.GetFullPath(bound).TrimEnd("\"c) & "\"
        Return IO.Path.GetFullPath(McFolderSelected & "versions\" & source("instanceName")?.ToString() & "\")
    End Function

    Public Sub UpdateSourceRuntime(source As JObject, releaseId As String, status As String, synced As Boolean)
        Dim sources = LoadSources()
        For Each item In sources.OfType(Of JObject)()
            If String.Equals(item("address")?.ToString(), source("address")?.ToString(), StringComparison.OrdinalIgnoreCase) AndAlso
               item("instanceId")?.ToString() = source("instanceId")?.ToString() Then
                item("lastReleaseId") = releaseId
                item("lastStatus") = status
                item("lastCheckedAt") = Date.Now.ToString("o")
                If synced Then item("lastSyncedAt") = Date.Now.ToString("o")
                SaveSources(sources)
                Return
            End If
        Next
    End Sub

    Public Sub AddSourceInteractive()
        Dim address = MyMsgBoxInput("添加同步源", "请输入同步服务根地址。支持 PassNat、FRP、端口映射、反向代理、内网地址或公网域名。", "http://", New ObjectModel.Collection(Of Validate))
        If String.IsNullOrWhiteSpace(address) Then Return
        Dim code = MyMsgBoxInput("添加同步源", "请输入长期同步码。同步码只保存在本机。", "", New ObjectModel.Collection(Of Validate))
        If String.IsNullOrWhiteSpace(code) Then Return
        Try
            address = NormalizeSourceAddress(address)
            Dim info = RequestJson(address, code, "source")
            If info("global") IsNot Nothing AndAlso CBool(info("global")) AndAlso TypeOf info("instances") Is JArray Then
                Dim available = DirectCast(info("instances"), JArray).OfType(Of JObject)().ToList()
                If available.Count = 0 Then Throw New Exception("统一同步码当前没有可用的已发布实例。")
                Dim choices As New List(Of IMyRadio)
                For Each item In available
                    choices.Add(New MyRadioBox With {.Text = item("instanceName").ToString()})
                Next
                Dim selected = MyMsgBoxSelect(choices, "选择同步服务器", "添加", "取消")
                If selected Is Nothing Then Return
                info = RequestJson(address, code, "source", CInt(available(selected.Value)("instanceId")))
            End If
            If info("instanceId") Is Nothing Then Throw New Exception("同步服务没有返回有效的服务器实例。")
            Dim instanceId = CInt(info("instanceId"))
            Dim manifest = RequestJson(address, code, "manifest", instanceId)
            Dim sources = LoadSources()
            For i = sources.Count - 1 To 0 Step -1
                If String.Equals(sources(i)("address")?.ToString(), address, StringComparison.OrdinalIgnoreCase) AndAlso
                   sources(i)("instanceId")?.ToString() = instanceId.ToString(Globalization.CultureInfo.InvariantCulture) Then sources.RemoveAt(i)
            Next
            Dim instanceName = info("instanceName").ToString(), versionFolder = IO.Path.GetFullPath(McFolderSelected & "versions\" & instanceName & "\")
            sources.Add(New JObject From {{"address", address}, {"code", code}, {"instanceId", instanceId}, {"instanceName", instanceName}, {"instancePath", versionFolder}})
            SaveSources(sources)
            If Not Directory.Exists(versionFolder) AndAlso MyMsgBox("已添加服务器：" & instanceName & vbCrLf & $"是否立即安装 Minecraft {manifest("minecraftVersion")} 与 {manifest("loader")}？", "首次安装", "立即安装", "稍后") = 1 Then
                Dim request As New McInstallRequest With {.NewInstanceName = instanceName, .VersionFolder = versionFolder, .MinecraftName = manifest("minecraftVersion").ToString()}
                Select Case manifest("loader")?.ToString().ToLowerInvariant()
                    Case "forge" : request.ForgeVersion = manifest("loaderVersion").ToString()
                    Case "neoforge" : request.NeoForgeVersion = manifest("loaderVersion").ToString()
                    Case "fabric" : request.FabricVersion = manifest("loaderVersion").ToString()
                    Case "quilt" : request.QuiltVersion = manifest("loaderVersion").ToString()
                End Select
                Directory.CreateDirectory(versionFolder & "PCL\")
                WriteIni(versionFolder & "PCL\Setup.ini", "VersionArgumentIndie", "1")
                If Not String.IsNullOrWhiteSpace(manifest("serverAddress")?.ToString()) Then WriteIni(versionFolder & "PCL\Setup.ini", "VersionServerEnter", manifest("serverAddress").ToString())
                McInstall(request)
            Else
                MyMsgBox("同步源添加成功。", "PCL IO")
            End If
        Catch ex As Exception
            MyMsgBox("无法连接同步源：" & ex.Message, "添加失败")
        End Try
    End Sub

    Public Sub ManageSourcesInteractive()
        Dim sources = LoadSources()
        If sources.Count = 0 Then MyMsgBox("尚未添加同步源。", "PCL IO") : Return
        Dim text = String.Join(vbCrLf, sources.Select(Function(x, i) $"{i + 1}. {x("instanceName")}  {x("address")}"))
        If MyMsgBox(text & vbCrLf & vbCrLf & "是否清空全部同步源？", "同步源管理", "保留", "全部清空") = 2 Then SaveSources(New JArray())
    End Sub

    Public Sub CheckIotaLauncherUpdate()
        Try
            Dim info As JObject
            Using client As New HttpClient()
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-IO/" & IotaLauncherVersion)
                info = JObject.Parse(client.GetStringAsync("https://api.github.com/repos/buziShui/iota-minecraft-sync/releases/latest").Result)
            End Using
            Dim remoteVersion = info("tag_name")?.ToString().TrimStart("v"c, "V"c)
            If String.IsNullOrWhiteSpace(remoteVersion) OrElse New Version(remoteVersion) <= New Version(IotaLauncherVersion) Then Return
            Dim notes = info("body")?.ToString()
            If notes IsNot Nothing AndAlso notes.Length > 300 Then notes = notes.Substring(0, 300) & "…"
            If MyMsgBox($"发现 PCL IO 正式版 {remoteVersion}。" & vbCrLf & notes, "启动器更新", "前往 GitHub 下载", "稍后") = 1 Then
                OpenWebsite(If(info("html_url")?.ToString(), "https://github.com/buziShui/iota-minecraft-sync/releases/latest"))
            End If
        Catch ex As Exception
            Logger.Warn(ex, "检查 GitHub 上的 PCL IO 更新失败")
        End Try
    End Sub

    Public Function BuildSyncPlan(source As JObject, manifest As JObject, Optional instanceRoot As String = Nothing) As JObject
        If String.IsNullOrWhiteSpace(instanceRoot) Then instanceRoot = ResolveInstancePath(source)
        instanceRoot = IO.Path.GetFullPath(instanceRoot).TrimEnd("\"c) & "\"
        If Not Directory.Exists(instanceRoot) Then Throw New DirectoryNotFoundException("绑定的 Minecraft 实例目录不存在：" & instanceRoot)
        Dim statePath = instanceRoot & ".iota-sync-state.json"
        Dim oldState As JObject = LoadSyncState(statePath)
        Dim oldFiles = DirectCast(oldState("files"), JObject)
        Dim remote = SelectRemoteFiles(source, manifest)
        Dim ignoredPaths = New HashSet(Of String)(If(TryCast(source("ignored"), JArray), New JArray()).Values(Of String)(), StringComparer.OrdinalIgnoreCase)
        Dim remotePaths As New HashSet(Of String)(remote.Select(Function(item) item("path").ToString()), StringComparer.OrdinalIgnoreCase)
        Dim added As New JArray(), updated As New JArray(), conflicts As New JArray(), deleted As New JArray(), extras As New JArray()
        Dim totalBytes As Long = 0

        For Each item In remote
            Dim relative = item("path").ToString()
            Dim target = SafeTargetPath(instanceRoot, relative)
            Dim expected = item("sha256").ToString()
            If Not File.Exists(target) Then
                added.Add(item.DeepClone()) : totalBytes += Math.Max(0, If(item("size")?.ToObject(Of Long)(), 0))
            Else
                Dim currentHash = Sha256(target)
                If Not String.Equals(currentHash, expected, StringComparison.OrdinalIgnoreCase) Then
                    Dim oldHash = oldFiles(relative)?.ToString()
                    Dim entry = DirectCast(item.DeepClone(), JObject)
                    entry("localSha256") = currentHash
                    If String.IsNullOrWhiteSpace(oldHash) OrElse Not String.Equals(currentHash, oldHash, StringComparison.OrdinalIgnoreCase) Then
                        entry("reason") = If(String.IsNullOrWhiteSpace(oldHash), "本地已有同名未托管文件", "本地文件在上次同步后被修改")
                        conflicts.Add(entry)
                    Else
                        updated.Add(entry)
                    End If
                    totalBytes += Math.Max(0, If(item("size")?.ToObject(Of Long)(), 0))
                End If
            End If
        Next

        For Each previous In oldFiles.Properties()
            If remotePaths.Contains(previous.Name) Then Continue For
            If ignoredPaths.Contains(previous.Name) Then Continue For
            Dim target = SafeTargetPath(instanceRoot, previous.Name)
            If Not File.Exists(target) Then Continue For
            Dim entry As New JObject From {{"path", previous.Name}, {"localSha256", Sha256(target)}, {"previousSha256", previous.Value.ToString()}}
            If String.Equals(entry("localSha256").ToString(), previous.Value.ToString(), StringComparison.OrdinalIgnoreCase) Then
                deleted.Add(entry)
            Else
                entry("reason") = "服务端已删除，但本地文件后来被修改"
                conflicts.Add(entry)
            End If
        Next

        Dim known As New HashSet(Of String)(remotePaths, StringComparer.OrdinalIgnoreCase)
        For Each previous In oldFiles.Properties() : known.Add(previous.Name) : Next
        For Each folder In remotePaths.Select(Function(path) path.Split("/"c)(0)).Distinct(StringComparer.OrdinalIgnoreCase)
            Dim scanRoot = SafeTargetPath(instanceRoot, folder)
            If Not Directory.Exists(scanRoot) Then Continue For
            For Each file In Directory.EnumerateFiles(scanRoot, "*", SearchOption.AllDirectories)
                Dim relative = file.Substring(instanceRoot.Length).Replace("\", "/")
                If Not known.Contains(relative) Then extras.Add(New JObject From {{"path", relative}, {"size", New FileInfo(file).Length}})
            Next
        Next

        Return New JObject From {
            {"source", source.DeepClone()}, {"manifest", manifest.DeepClone()}, {"instanceRoot", instanceRoot},
            {"state", oldState}, {"added", added}, {"updated", updated}, {"conflicts", conflicts},
            {"deleted", deleted}, {"extra", extras}, {"downloadBytes", totalBytes}
        }
    End Function

    Private Function SelectRemoteFiles(source As JObject, manifest As JObject) As List(Of JObject)
        Dim optionalState = TryCast(source("optional"), JObject)
        If optionalState Is Nothing Then optionalState = New JObject()
        Dim ignored = New HashSet(Of String)(
            If(TryCast(source("ignored"), JArray), New JArray()).Values(Of String)(),
            StringComparer.OrdinalIgnoreCase)
        Return DirectCast(manifest("files"), JArray).OfType(Of JObject)().
            Where(Function(item)
                      Dim relative = item("path").ToString()
                      Dim optionalFile = CInt(item("side")) = 1
                      If Not optionalFile Then Return True
                      If ignored.Contains(relative) Then Return False
                      Return optionalState(relative) Is Nothing OrElse CBool(optionalState(relative))
                  End Function).ToList()
    End Function

    Private Function LoadSyncState(statePath As String) As JObject
        Try
            If File.Exists(statePath) Then
                Dim state = JObject.Parse(File.ReadAllText(statePath))
                If TypeOf state("files") Is JObject Then Return state
            End If
        Catch ex As Exception
            Logger.Warn(ex, "读取 Iota 同步状态失败，将重新建立状态")
        End Try
        Return New JObject From {{"files", New JObject()}}
    End Function

    Public Function SyncPlanSummary(plan As JObject, Optional includeFiles As Boolean = True) As String
        Dim added = DirectCast(plan("added"), JArray), updated = DirectCast(plan("updated"), JArray)
        Dim deleted = DirectCast(plan("deleted"), JArray), conflicts = DirectCast(plan("conflicts"), JArray)
        Dim extras = DirectCast(plan("extra"), JArray)
        Dim text = $"新增 {added.Count} · 更新 {updated.Count} · 服务端删除 {deleted.Count} · 本地冲突 {conflicts.Count} · 玩家新增 {extras.Count}" &
                   $" · 下载 {FormatIotaSize(CLng(plan("downloadBytes")))}"
        If Not includeFiles Then Return text
        Dim details As New List(Of String)
        AddPlanLines(details, "新增", added)
        AddPlanLines(details, "更新", updated)
        AddPlanLines(details, "删除", deleted)
        AddPlanLines(details, "冲突", conflicts)
        AddPlanLines(details, "保留", extras)
        If details.Count > 0 Then text &= vbCrLf & vbCrLf & String.Join(vbCrLf, details.Take(45))
        If details.Count > 45 Then text &= vbCrLf & $"……另有 {details.Count - 45} 项，请在同步页面查看。"
        Return text
    End Function

    Private Sub AddPlanLines(output As List(Of String), label As String, items As JArray)
        For Each item In items.OfType(Of JObject)()
            output.Add($"[{label}] {item("path")}")
        Next
    End Sub

    Public Sub RunManualSync(source As JObject)
        Dim current = DirectCast(source.DeepClone(), JObject)
        RunInNewThread(
            Sub()
                Try
                    Dim manifest = GetSourceManifest(current)
                    ExecuteSync(current, manifest, ResolveInstancePath(current), True)
                Catch ex As OperationCanceledException
                    'ExecuteSync 已记录取消结果。
                Catch ex As Exception
                    RunInUi(Sub() Hint("同步失败：" & ex.Message, HintType.Red))
                End Try
            End Sub,
            "Iota Manual Sync")
    End Sub

    Public Sub SyncBeforeLaunch()
        Dim selectedRoot = IO.Path.GetFullPath(McInstanceSelected.PathIndie).TrimEnd("\"c) & "\"
        Dim source = LoadSources().OfType(Of JObject)().FirstOrDefault(
            Function(item)
                Dim bound = item("instancePath")?.ToString()
                Return Not String.IsNullOrWhiteSpace(bound) AndAlso
                       String.Equals(IO.Path.GetFullPath(bound).TrimEnd("\"c) & "\", selectedRoot, StringComparison.OrdinalIgnoreCase)
            End Function)
        If source Is Nothing Then
            source = LoadSources().OfType(Of JObject)().FirstOrDefault(Function(item) String.Equals(item("instanceName")?.ToString(), McInstanceSelected.Name, StringComparison.OrdinalIgnoreCase))
        End If
        If source Is Nothing Then Return
        Dim manifest = GetSourceManifest(source)
        ExecuteSync(source, manifest, selectedRoot, True)
    End Sub

    Private Sub ExecuteSync(source As JObject, manifest As JObject, instanceRoot As String, askConfirmation As Boolean)
        Dim releaseId = manifest("releaseId")?.ToString()
        UpdateSourceRuntime(source, releaseId, "正在扫描本地差异", False)
        SyncCancellation = New CancellationTokenSource()
        SyncCompletedBytes = 0
        SyncStartedAt = Date.Now
        LastFailedSource = Nothing
        ReportSyncProgress("正在扫描本地文件……")
        Try
            Dim plan = BuildSyncPlan(source, manifest, instanceRoot)
            SyncTotalBytes = CLng(plan("downloadBytes"))
            Dim changes = DirectCast(plan("added"), JArray).Count + DirectCast(plan("updated"), JArray).Count +
                          DirectCast(plan("deleted"), JArray).Count + DirectCast(plan("conflicts"), JArray).Count
            If changes = 0 Then
                SavePlanState(plan, DirectCast(plan("state")("files"), JObject))
                UpdateSourceRuntime(source, releaseId, "客户端已是最新版本", True)
                AppendHistory(source, releaseId, "成功", 0, 0, "文件一致")
                ReportSyncProgress("客户端已是最新版本。", completed:=True)
                Return
            End If

            If askConfirmation AndAlso MyMsgBox(
                SyncPlanSummary(plan, True) & vbCrLf & vbCrLf & "是否应用以上更新？玩家自行添加的文件不会删除。",
                "PCL IO 更新差异预览", "确认同步", "取消", IsWarn:=DirectCast(plan("conflicts"), JArray).Count > 0) <> 1 Then
                Throw New OperationCanceledException("玩家取消了同步")
            End If

            Dim conflictMode = 1
            If DirectCast(plan("conflicts"), JArray).Count > 0 Then
                Dim conflictText = "检测到本地修改或同名文件冲突，共 " & DirectCast(plan("conflicts"), JArray).Count & " 项。" &
                                   vbCrLf & "选择使用服务端版本会先创建恢复点；选择保留本地版本会跳过全部冲突。"
                conflictMode = MyMsgBox(conflictText, "冲突处理中心", "使用服务端版本", "保留本地版本", "取消同步", IsWarn:=True)
                If conflictMode = 3 Then Throw New OperationCanceledException("玩家取消了冲突处理")
            End If

            ApplySyncPlan(plan, conflictMode = 1)
            Dim elapsed = Math.Max(0.01, (Date.Now - SyncStartedAt).TotalSeconds)
            UpdateSourceRuntime(source, releaseId, $"同步完成，共处理 {changes} 个文件", True)
            AppendHistory(source, releaseId, "成功", changes, SyncCompletedBytes, $"耗时 {Math.Round(elapsed, 1)} 秒")
            ReportSyncProgress($"同步完成：{changes} 个文件，{FormatIotaSize(SyncCompletedBytes)}。", completed:=True)
        Catch ex As OperationCanceledException
            AppendHistory(source, releaseId, "已取消", 0, SyncCompletedBytes, ex.Message)
            ReportSyncProgress("同步任务已取消，可稍后重试并续传。", completed:=True)
            Throw
        Catch ex As Exception
            LastFailedSource = DirectCast(source.DeepClone(), JObject)
            AppendHistory(source, releaseId, "失败", 0, SyncCompletedBytes, ex.Message)
            ReportSyncProgress("同步失败：" & ex.Message, failed:=True, completed:=True)
            Throw
        Finally
            SyncCancellation.Dispose()
            SyncCancellation = Nothing
        End Try
    End Sub

    Private Sub ApplySyncPlan(plan As JObject, overwriteConflicts As Boolean)
        Dim source = DirectCast(plan("source"), JObject)
        Dim manifest = DirectCast(plan("manifest"), JObject)
        Dim instanceRoot = plan("instanceRoot").ToString()
        Dim oldState = DirectCast(plan("state"), JObject)
        Dim oldFiles = DirectCast(oldState("files"), JObject)
        Dim newFiles As New JObject()
        Dim backupRoot = instanceRoot & ".iota-backup\" & Date.Now.ToString("yyyyMMdd-HHmmss-fff") & "\"
        Dim restoreEntries As New JArray()

        For Each item In SelectRemoteFiles(source, manifest)
            SyncCancellation.Token.ThrowIfCancellationRequested()
            Dim relative = item("path").ToString()
            Dim target = SafeTargetPath(instanceRoot, relative)
            Dim expected = item("sha256").ToString()
            Dim isConflict = DirectCast(plan("conflicts"), JArray).OfType(Of JObject)().Any(Function(entry) entry("path")?.ToString() = relative)
            If isConflict AndAlso Not overwriteConflicts Then
                If File.Exists(target) Then newFiles(relative) = Sha256(target)
                Continue For
            End If
            If Not FileMatches(target, expected) Then
                PrepareRestoreEntry(target, instanceRoot, backupRoot, relative, restoreEntries)
                DownloadFile(source, relative, target, expected, If(item("size")?.ToObject(Of Long)(), 0))
            End If
            newFiles(relative) = expected
        Next

        For Each item In DirectCast(plan("deleted"), JArray).OfType(Of JObject)()
            SyncCancellation.Token.ThrowIfCancellationRequested()
            Dim relative = item("path").ToString(), target = SafeTargetPath(instanceRoot, relative)
            If File.Exists(target) AndAlso String.Equals(Sha256(target), item("previousSha256")?.ToString(), StringComparison.OrdinalIgnoreCase) Then
                PrepareRestoreEntry(target, instanceRoot, backupRoot, relative, restoreEntries)
                File.Delete(target)
            End If
        Next

        If Not overwriteConflicts Then
            For Each item In DirectCast(plan("conflicts"), JArray).OfType(Of JObject)()
                Dim relative = item("path").ToString(), target = SafeTargetPath(instanceRoot, relative)
                If File.Exists(target) Then newFiles(relative) = Sha256(target)
            Next
        End If
        PrepareRestoreEntry(instanceRoot & ".iota-sync-state.json", instanceRoot, backupRoot, ".iota-sync-state.json", restoreEntries)
        SavePlanState(plan, newFiles)
        If restoreEntries.Count > 0 Then
            Directory.CreateDirectory(backupRoot)
            Dim metadata As New JObject From {
                {"createdAt", Date.Now.ToString("o")}, {"instanceName", source("instanceName")},
                {"fromRelease", oldState("releaseId")}, {"toRelease", manifest("releaseId")},
                {"instanceRoot", instanceRoot}, {"entries", restoreEntries}
            }
            File.WriteAllText(backupRoot & "restore.json", metadata.ToString(Newtonsoft.Json.Formatting.Indented))
        End If
    End Sub

    Private Sub PrepareRestoreEntry(target As String, instanceRoot As String, backupRoot As String, relative As String, entries As JArray)
        If entries.OfType(Of JObject)().Any(Function(entry) entry("path")?.ToString() = relative) Then Return
        If File.Exists(target) Then
            BackupFile(target, instanceRoot, backupRoot)
            entries.Add(New JObject From {{"path", relative}, {"action", "restore"}})
        Else
            entries.Add(New JObject From {{"path", relative}, {"action", "delete"}})
        End If
    End Sub

    Private Sub SavePlanState(plan As JObject, files As JObject)
        Dim statePath = plan("instanceRoot").ToString() & ".iota-sync-state.json"
        Dim state As New JObject From {{"releaseId", plan("manifest")("releaseId")}, {"files", files}, {"syncedAt", Date.Now.ToString("o")}}
        File.WriteAllText(statePath, state.ToString(Newtonsoft.Json.Formatting.Indented))
    End Sub

    Private Function RequestJson(address As String, code As String, endpoint As String, Optional instanceId As Integer? = Nothing) As JObject
        Using client As New HttpClient()
            client.Timeout = TimeSpan.FromSeconds(30) : client.DefaultRequestHeaders.Add("X-Iota-Sync-Code", code)
            Dim url = address.TrimEnd("/"c) & "/api/plugins/mslx-plugin-iota-sync/sync/" & endpoint
            If instanceId.HasValue Then url &= "?instanceId=" & instanceId.Value.ToString(Globalization.CultureInfo.InvariantCulture)
            Dim response = client.GetAsync(url).Result
            response.EnsureSuccessStatusCode() : Return JObject.Parse(response.Content.ReadAsStringAsync().Result)
        End Using
    End Function

    Private Sub DownloadFile(source As JObject, relative As String, target As String, expected As String, expectedSize As Long)
        Directory.CreateDirectory(IO.Path.GetDirectoryName(target))
        Dim temp = target & ".iota-downloading"
        Dim url = source("address").ToString().TrimEnd("/"c) & "/api/plugins/mslx-plugin-iota-sync/sync/files/" &
                  String.Join("/", relative.Split("/"c).Select(Function(x) Uri.EscapeDataString(x))) &
                  "?instanceId=" & CInt(source("instanceId")).ToString(Globalization.CultureInfo.InvariantCulture)
        Dim lastError As Exception = Nothing
        For attempt = 1 To 3
            SyncCancellation.Token.ThrowIfCancellationRequested()
            Try
                Dim existing = If(File.Exists(temp), New FileInfo(temp).Length, 0)
                If expectedSize > 0 AndAlso existing > expectedSize Then File.Delete(temp) : existing = 0
                Using client As New HttpClient()
                    client.Timeout = TimeSpan.FromMinutes(10)
                    client.DefaultRequestHeaders.Add("X-Iota-Sync-Code", source("code").ToString())
                    If existing > 0 Then client.DefaultRequestHeaders.Range = New RangeHeaderValue(existing, Nothing)
                    Using response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, SyncCancellation.Token).GetAwaiter().GetResult()
                        response.EnsureSuccessStatusCode()
                        Dim append = existing > 0 AndAlso response.StatusCode = Net.HttpStatusCode.PartialContent
                        If Not append Then existing = 0
                        Using input = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult(),
                              output As New FileStream(temp, If(append, FileMode.Append, FileMode.Create), FileAccess.Write, FileShare.None)
                            Dim buffer(131071) As Byte
                            Do
                                SyncCancellation.Token.ThrowIfCancellationRequested()
                                Dim count = input.Read(buffer, 0, buffer.Length)
                                If count <= 0 Then Exit Do
                                output.Write(buffer, 0, count)
                                SyncCompletedBytes += count
                                Dim elapsed = Math.Max(0.1, (Date.Now - SyncStartedAt).TotalSeconds)
                                Dim remaining = Math.Max(0, SyncTotalBytes - SyncCompletedBytes)
                                ReportSyncProgress($"正在下载 {relative} · {FormatIotaSize(SyncCompletedBytes / elapsed)}/s · 剩余 {FormatIotaSize(remaining)}")
                            Loop
                        End Using
                    End Using
                End Using
                If Not String.Equals(Sha256(temp), expected, StringComparison.OrdinalIgnoreCase) Then
                    File.Delete(temp)
                    Throw New Exception("文件校验失败")
                End If
                lastError = Nothing
                Exit For
            Catch ex As OperationCanceledException
                Throw
            Catch ex As Exception
                lastError = ex
                Logger.Warn(ex, $"下载 {relative} 第 {attempt} 次失败")
                ReportSyncProgress($"下载失败，正在重试（{attempt}/3）：{relative}")
            End Try
        Next
        If lastError IsNot Nothing Then Throw New Exception($"下载失败：{relative}：{lastError.Message}", lastError)
        If File.Exists(target) Then File.Delete(target)
        File.Move(temp, target)
    End Sub

    Public Function LoadIotaHistory() As JArray
        SyncLock ConfigLock
            Try
                If File.Exists(HistoryPath) Then Return JArray.Parse(File.ReadAllText(HistoryPath))
            Catch ex As Exception
                Logger.Warn(ex, "读取 Iota 同步历史失败")
            End Try
            Return New JArray()
        End SyncLock
    End Function

    Private Sub AppendHistory(source As JObject, releaseId As String, result As String, fileCount As Integer, byteCount As Long, message As String)
        Dim history = LoadIotaHistory()
        history.Insert(0, New JObject From {
            {"time", Date.Now.ToString("o")}, {"instanceName", source?("instanceName")},
            {"address", source?("address")}, {"instanceId", source?("instanceId")},
            {"releaseId", releaseId}, {"result", result}, {"fileCount", fileCount},
            {"bytes", byteCount}, {"message", message}
        })
        While history.Count > 200 : history.RemoveAt(history.Count - 1) : End While
        SyncLock ConfigLock
            Directory.CreateDirectory(IO.Path.GetDirectoryName(HistoryPath))
            File.WriteAllText(HistoryPath, history.ToString(Newtonsoft.Json.Formatting.Indented))
        End SyncLock
    End Sub

    Public Function TestSourceConnection(source As JObject) As JObject
        Dim timer = Diagnostics.Stopwatch.StartNew()
        Try
            Dim info = RequestJson(source("address").ToString(), source("code").ToString(), "source", CInt(source("instanceId")))
            Dim manifest = RequestJson(source("address").ToString(), source("code").ToString(), "manifest", CInt(source("instanceId")))
            timer.Stop()
            Return New JObject From {
                {"ok", True}, {"latency", timer.ElapsedMilliseconds}, {"instanceName", info("instanceName")},
                {"releaseId", manifest("releaseId")}, {"files", DirectCast(manifest("files"), JArray).Count},
                {"message", "地址、同步码、实例与 manifest 清单均可正常访问"}
            }
        Catch ex As Exception
            timer.Stop()
            Return New JObject From {{"ok", False}, {"latency", timer.ElapsedMilliseconds}, {"message", ex.Message}}
        End Try
    End Function

    Public Function LoadBackupPoints(source As JObject) As JArray
        Dim result As New JArray()
        Dim root = ResolveInstancePath(source) & ".iota-backup\"
        If Not Directory.Exists(root) Then Return result
        For Each metadataPath In Directory.EnumerateFiles(root, "restore.json", SearchOption.AllDirectories)
            Try
                Dim metadata = JObject.Parse(File.ReadAllText(metadataPath))
                metadata("metadataPath") = metadataPath
                result.Add(metadata)
            Catch ex As Exception
                Logger.Warn(ex, "跳过损坏的 Iota 恢复点：" & metadataPath)
            End Try
        Next
        Return New JArray(result.OfType(Of JObject)().OrderByDescending(Function(item) item("createdAt")?.ToString()))
    End Function

    Public Sub RestoreBackupPoint(metadataPath As String)
        Dim metadata = JObject.Parse(File.ReadAllText(metadataPath))
        Dim instanceRoot = IO.Path.GetFullPath(metadata("instanceRoot").ToString()).TrimEnd("\"c) & "\"
        Dim backupRoot = IO.Path.GetDirectoryName(metadataPath).TrimEnd("\"c) & "\"
        For Each entry In DirectCast(metadata("entries"), JArray).OfType(Of JObject)()
            Dim relative = entry("path").ToString()
            Dim target = SafeTargetPath(instanceRoot, relative)
            If entry("action")?.ToString() = "delete" Then
                If File.Exists(target) Then File.Delete(target)
            Else
                Dim backup = SafeTargetPath(backupRoot, relative)
                If Not File.Exists(backup) Then Throw New FileNotFoundException("恢复点缺少文件：" & relative)
                Directory.CreateDirectory(IO.Path.GetDirectoryName(target))
                File.Copy(backup, target, True)
            End If
        Next
        AppendHistory(New JObject From {{"instanceName", metadata("instanceName")}}, metadata("fromRelease")?.ToString(), "已恢复", DirectCast(metadata("entries"), JArray).Count, 0, "已恢复到同步前状态")
    End Sub

    Public Function BuildDiagnosticReport(source As JObject) As String
        Dim builder As New StringBuilder()
        builder.AppendLine("PCL IO 同步诊断报告")
        builder.AppendLine("生成时间：" & Date.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))
        builder.AppendLine("启动器版本：" & IotaLauncherVersion)
        builder.AppendLine("系统：" & Environment.OSVersion.ToString())
        builder.AppendLine("运行库：" & Environment.Version.ToString())
        If source IsNot Nothing Then
            builder.AppendLine("同步源：" & source("address")?.ToString())
            builder.AppendLine("实例：" & source("instanceName")?.ToString() & " (" & source("instanceId")?.ToString() & ")")
            builder.AppendLine("绑定目录：" & ResolveInstancePath(source))
            builder.AppendLine("同步码：***（已脱敏，长度 " & If(source("code")?.ToString().Length, 0) & "）")
            Dim health = TestSourceConnection(source)
            builder.AppendLine("连接检查：" & If(CBool(health("ok")), "通过", "失败") & "，" & health("latency").ToString() & " ms，" & health("message")?.ToString())
            Try
                Dim manifest = GetSourceManifest(source)
                Dim plan = BuildSyncPlan(source, manifest)
                builder.AppendLine("当前差异：" & SyncPlanSummary(plan, False))
                For Each category In {"added", "updated", "deleted", "conflicts", "extra"}
                    For Each item In DirectCast(plan(category), JArray).OfType(Of JObject)()
                        builder.AppendLine($"[{category}] {item("path")}")
                    Next
                Next
            Catch ex As Exception
                builder.AppendLine("差异扫描失败：" & ex.Message)
            End Try
        End If
        builder.AppendLine()
        builder.AppendLine("最近同步历史：")
        For Each item In LoadIotaHistory().OfType(Of JObject)().Take(30)
            builder.AppendLine($"{item("time")} | {item("instanceName")} | {item("releaseId")} | {item("result")} | {item("message")}")
        Next
        Return builder.ToString()
    End Function

    Public Function FormatIotaSize(size As Double) As String
        If size < 1024 Then Return Math.Round(size) & " B"
        If size < 1024 * 1024 Then Return Math.Round(size / 1024, 1) & " KB"
        If size < 1024 * 1024 * 1024 Then Return Math.Round(size / 1024 / 1024, 1) & " MB"
        Return Math.Round(size / 1024 / 1024 / 1024, 2) & " GB"
    End Function

    Private Function SafeTargetPath(root As String, relative As String) As String
        root = IO.Path.GetFullPath(root).TrimEnd("\"c) & "\"
        Dim target = IO.Path.GetFullPath(IO.Path.Combine(root, relative.Replace("/", "\")))
        If Not target.StartsWith(root, StringComparison.OrdinalIgnoreCase) Then Throw New IOException("清单包含非法路径：" & relative)
        Return target
    End Function

    Private Sub BackupFile(file As String, root As String, backupRoot As String)
        Dim relative = file.Substring(root.Length), target = backupRoot & relative
        Directory.CreateDirectory(IO.Path.GetDirectoryName(target)) : IO.File.Copy(file, target, True)
    End Sub
    Private Function FileMatches(file As String, expected As String) As Boolean
        Return IO.File.Exists(file) AndAlso String.Equals(Sha256(file), expected, StringComparison.OrdinalIgnoreCase)
    End Function
    Private Function Sha256(file As String) As String
        Using stream = IO.File.OpenRead(file), sha = Security.Cryptography.SHA256.Create()
            Return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant()
        End Using
    End Function

    Private Function LoadSourcesWithReplacement(updated As JObject) As JArray
        Dim sources = LoadSources()
        For i = 0 To sources.Count - 1
            If String.Equals(sources(i)("address")?.ToString(), updated("address")?.ToString(), StringComparison.OrdinalIgnoreCase) AndAlso
               sources(i)("instanceId")?.ToString() = updated("instanceId")?.ToString() Then sources(i) = updated : Exit For
        Next
        Return sources
    End Function
End Module
