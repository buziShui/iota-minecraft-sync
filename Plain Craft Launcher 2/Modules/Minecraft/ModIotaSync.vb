Imports System.Net.Http
Imports System.Security.Cryptography
Imports Newtonsoft.Json.Linq

Public Module ModIotaSync
    Public Const IotaLauncherVersion As String = "0.1.0"
    Private ReadOnly ConfigPath As String = Paths.Base & "PCL\IotaSync.json"

    Public Function LoadSources() As JArray
        Try
            If File.Exists(ConfigPath) Then Return JArray.Parse(File.ReadAllText(ConfigPath))
        Catch ex As Exception
            Logger.Warn(ex, "读取 Iota 同步源失败")
        End Try
        Return New JArray()
    End Function

    Private Sub SaveSources(sources As JArray)
        Directory.CreateDirectory(IO.Path.GetDirectoryName(ConfigPath))
        File.WriteAllText(ConfigPath, sources.ToString(Newtonsoft.Json.Formatting.Indented))
    End Sub

    Public Sub AddSourceInteractive()
        Dim address = MyMsgBoxInput("添加同步源", "请输入 PassNat 映射地址，例如 http://example:12345", "http://", New ObjectModel.Collection(Of Validate))
        If String.IsNullOrWhiteSpace(address) Then Return
        Dim code = MyMsgBoxInput("添加同步源", "请输入长期同步码。同步码只保存在本机。", "", New ObjectModel.Collection(Of Validate))
        If String.IsNullOrWhiteSpace(code) Then Return
        Try
            Dim info = RequestJson(address, code, "source")
            Dim sources = LoadSources()
            sources.Add(New JObject From {{"address", address.TrimEnd("/"c)}, {"code", code}, {"instanceId", info("instanceId")}, {"instanceName", info("instanceName")}})
            SaveSources(sources)
            Dim manifest = RequestJson(address, code, "manifest")
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
        Dim source = LoadSources().OfType(Of JObject)().FirstOrDefault()
        If source Is Nothing Then Return
        Try
            Dim info = RequestJson(source("address").ToString(), source("code").ToString(), "launcher")
            Dim remoteVersion = info("version")?.ToString()
            If String.IsNullOrWhiteSpace(remoteVersion) OrElse New Version(remoteVersion) <= New Version(IotaLauncherVersion) Then Return
            If MyMsgBox($"发现 PCL IO 正式版 {remoteVersion}。" & vbCrLf & info("notes")?.ToString(), "启动器更新", "下载并重启", "稍后") <> 1 Then Return
            Dim current = Process.GetCurrentProcess().MainModule.FileName, temp = current & ".iota-update"
            Using client As New HttpClient()
                client.DefaultRequestHeaders.Add("X-Iota-Sync-Code", source("code").ToString())
                Using input = client.GetStreamAsync(source("address").ToString().TrimEnd("/"c) & "/api/plugins/mslx-plugin-iota-sync/sync/launcher/file").Result, output = File.Create(temp)
                    input.CopyTo(output)
                End Using
            End Using
            If Sha256(temp) <> info("sha256").ToString() Then File.Delete(temp) : Throw New Exception("启动器更新包校验失败")
            Dim updater = Paths.Base & "PCL\IotaUpdate.cmd"
            File.WriteAllText(updater, "@echo off" & vbCrLf & "ping 127.0.0.1 -n 3 >nul" & vbCrLf & $"copy /y ""{temp}"" ""{current}"" >nul" & vbCrLf & $"start """" ""{current}""" & vbCrLf & "del /q ""%~f0""", Text.Encoding.Default)
            Process.Start(New ProcessStartInfo("cmd.exe", "/c """ & updater & """") With {.CreateNoWindow = True, .WindowStyle = ProcessWindowStyle.Hidden})
            RunInUi(Sub() Application.Current.Shutdown())
        Catch ex As Exception
            Logger.Warn(ex, "检查 PCL IO 更新失败")
        End Try
    End Sub

    Public Sub SyncBeforeLaunch()
        Dim source = LoadSources().OfType(Of JObject)().FirstOrDefault(Function(x) String.Equals(x("instanceName")?.ToString(), McInstanceSelected.Name, StringComparison.OrdinalIgnoreCase))
        If source Is Nothing Then Return
        Dim manifest = RequestJson(source("address").ToString(), source("code").ToString(), "manifest")
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

    Private Function RequestJson(address As String, code As String, endpoint As String) As JObject
        Using client As New HttpClient()
            client.Timeout = TimeSpan.FromSeconds(30) : client.DefaultRequestHeaders.Add("X-Iota-Sync-Code", code)
            Dim response = client.GetAsync(address.TrimEnd("/"c) & "/api/plugins/mslx-plugin-iota-sync/sync/" & endpoint).Result
            response.EnsureSuccessStatusCode() : Return JObject.Parse(response.Content.ReadAsStringAsync().Result)
        End Using
    End Function

    Private Sub DownloadFile(source As JObject, relative As String, target As String, expected As String)
        Directory.CreateDirectory(IO.Path.GetDirectoryName(target))
        Dim temp = target & ".iota-downloading"
        Using client As New HttpClient()
            client.DefaultRequestHeaders.Add("X-Iota-Sync-Code", source("code").ToString())
            Dim url = source("address").ToString().TrimEnd("/"c) & "/api/plugins/mslx-plugin-iota-sync/sync/files/" & String.Join("/", relative.Split("/"c).Select(Function(x) Uri.EscapeDataString(x)))
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
            If sources(i)("instanceId")?.ToString() = updated("instanceId")?.ToString() Then sources(i) = updated : Exit For
        Next
        Return sources
    End Function
End Module
