Imports System.Net.Http
Imports System.Security.Cryptography
Imports Newtonsoft.Json.Linq

Public Module ModIotaSync
    Public Const IotaLauncherVersion As String = "1.2.2"
    Private ReadOnly ConfigPath As String = Paths.Base & "PCL\IotaSync.json"
    Private ReadOnly ConfigLock As New Object

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
            sources.Add(New JObject From {{"address", address}, {"code", code}, {"instanceId", instanceId}, {"instanceName", info("instanceName")}})
            SaveSources(sources)
            Dim instanceName = info("instanceName").ToString(), versionFolder = McFolderSelected & "versions\" & instanceName & "\"
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

    Public Sub SyncBeforeLaunch()
        Dim source = LoadSources().OfType(Of JObject)().FirstOrDefault(Function(x) String.Equals(x("instanceName")?.ToString(), McInstanceSelected.Name, StringComparison.OrdinalIgnoreCase))
        If source Is Nothing Then Return
        Dim manifest = RequestJson(source("address").ToString(), source("code").ToString(), "manifest", CInt(source("instanceId")))
        Dim releaseId = manifest("releaseId")?.ToString()
        Dim statePath = McInstanceSelected.PathIndie & ".iota-sync-state.json"
        Dim oldState As JObject = If(File.Exists(statePath), JObject.Parse(File.ReadAllText(statePath)), New JObject From {{"files", New JObject()}})
        If oldState("releaseId")?.ToString() = releaseId Then Return
        Dim optionalState = TryCast(source("optional"), JObject)
        If optionalState Is Nothing Then optionalState = New JObject() : source("optional") = optionalState
        Dim optionChanged As Boolean = False
        For Each optionalFile In DirectCast(manifest("files"), JArray).OfType(Of JObject)().Where(Function(x) CInt(x("side")) = 1)
            Dim relative = optionalFile("path").ToString()
            If optionalState(relative) Is Nothing Then
                optionalState(relative) = (MyMsgBox("可选内容：" & relative & vbCrLf & "是否安装？默认建议安装。", "PCL IO 可选内容", "安装", "不安装") = 1)
                optionChanged = True
            End If
        Next
        If optionChanged Then SaveSources(LoadSourcesWithReplacement(source))
        Dim remote = DirectCast(manifest("files"), JArray).OfType(Of JObject)().Where(Function(x) CInt(x("side")) = 0 OrElse (CInt(x("side")) = 1 AndAlso CBool(optionalState(x("path").ToString())))).ToList()
        Dim changes = remote.Where(Function(x) Not FileMatches(McInstanceSelected.PathIndie & x("path").ToString().Replace("/", "\"), x("sha256").ToString())).Count()
        If changes = 0 Then Return
        If MyMsgBox($"服务器 {manifest("instanceName")} 有新版本 {releaseId}，需要更新 {changes} 个文件。" & vbCrLf & "玩家自行添加的 Mod 会保留并仅在日志中提示。", "PCL IO 同步", "立即更新", "取消启动") <> 1 Then Throw New OperationCanceledException()
        Dim backupRoot = McInstanceSelected.PathIndie & ".iota-backup\" & Date.Now.ToString("yyyyMMdd-HHmmss") & "\"
        Dim newFiles As New JObject()
        For Each item In remote
            Dim relative = item("path").ToString(), target = McInstanceSelected.PathIndie & relative.Replace("/", "\")
            Dim expected = item("sha256").ToString(), oldHash = oldState("files")?(relative)?.ToString()
            If File.Exists(target) AndAlso oldHash IsNot Nothing AndAlso Sha256(target) <> oldHash AndAlso Sha256(target) <> expected Then
                If MyMsgBox("本地配置已修改：" & relative & vbCrLf & "是否使用服务器版本覆盖？本地文件会先备份。", "配置冲突", "覆盖", "保留本地") = 2 Then newFiles(relative) = Sha256(target) : Continue For
            End If
            If File.Exists(target) Then BackupFile(target, McInstanceSelected.PathIndie, backupRoot)
            If Not FileMatches(target, expected) Then DownloadFile(source, relative, target, expected)
            newFiles(relative) = expected
        Next
        For Each propertyItem In DirectCast(oldState("files"), JObject).Properties()
            If newFiles(propertyItem.Name) Is Nothing Then
                Dim target = McInstanceSelected.PathIndie & propertyItem.Name.Replace("/", "\")
                If File.Exists(target) AndAlso Sha256(target) = propertyItem.Value.ToString() Then BackupFile(target, McInstanceSelected.PathIndie, backupRoot) : File.Delete(target)
            End If
        Next
        File.WriteAllText(statePath, (New JObject From {{"releaseId", releaseId}, {"files", newFiles}}).ToString(Newtonsoft.Json.Formatting.Indented))
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

    Private Sub DownloadFile(source As JObject, relative As String, target As String, expected As String)
        Directory.CreateDirectory(IO.Path.GetDirectoryName(target))
        Dim temp = target & ".iota-downloading"
        Using client As New HttpClient()
            client.DefaultRequestHeaders.Add("X-Iota-Sync-Code", source("code").ToString())
            Dim url = source("address").ToString().TrimEnd("/"c) & "/api/plugins/mslx-plugin-iota-sync/sync/files/" & String.Join("/", relative.Split("/"c).Select(Function(x) Uri.EscapeDataString(x)))
            url &= "?instanceId=" & CInt(source("instanceId")).ToString(Globalization.CultureInfo.InvariantCulture)
            Using input = client.GetStreamAsync(url).Result, output = File.Create(temp) : input.CopyTo(output) : End Using
        End Using
        If Sha256(temp) <> expected Then File.Delete(temp) : Throw New Exception("文件校验失败：" & relative)
        If File.Exists(target) Then File.Delete(target)
        File.Move(temp, target)
    End Sub

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
