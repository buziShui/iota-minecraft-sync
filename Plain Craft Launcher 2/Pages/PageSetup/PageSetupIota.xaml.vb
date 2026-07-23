Imports Newtonsoft.Json.Linq

Public Class PageSetupIota
    Private CurrentSource As JObject
    Private CurrentManifest As JObject
    Private CurrentPlan As JObject
    Private CurrentOptional As JObject
    Private CurrentIgnored As JArray
    Private CurrentModFiles As New List(Of JObject)
    Private ReadOnly OptionalChecks As New List(Of MyCheckBox)
    Private ReadOnly IgnoreChecks As New List(Of MyCheckBox)
    Private ProgressSubscribed As Boolean

    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If Not ProgressSubscribed Then
            AddHandler IotaSyncProgressChanged, AddressOf OnSyncProgressChanged
            ProgressSubscribed = True
        End If
        SelectIotaTab(If(TabMods.Checked, 1, If(TabSync.Checked, 2, If(TabRecords.Checked, 3, 0))))
        PanBack.ScrollToHome()
        RefreshSources()
        RefreshOverview()
        RefreshHistory()
    End Sub

    Private Sub Page_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        If ProgressSubscribed Then
            RemoveHandler IotaSyncProgressChanged, AddressOf OnSyncProgressChanged
            ProgressSubscribed = False
        End If
    End Sub

    Private Sub Tab_Checked(sender As MyRadioButton, raiseByMouse As Boolean) Handles TabOverview.Check, TabMods.Check, TabSync.Check, TabRecords.Check
        If sender.Tag Is Nothing OrElse PanTabOverview Is Nothing Then Return
        SelectIotaTab(CInt(sender.Tag))
    End Sub

    Private Sub SelectIotaTab(index As Integer)
        PanTabOverview.Visibility = If(index = 0, Visibility.Visible, Visibility.Collapsed)
        PanTabMods.Visibility = If(index = 1, Visibility.Visible, Visibility.Collapsed)
        PanTabSync.Visibility = If(index = 2, Visibility.Visible, Visibility.Collapsed)
        PanTabRecords.Visibility = If(index = 3, Visibility.Visible, Visibility.Collapsed)
        If IsLoaded Then PanBack.ScrollToHome()
    End Sub

    Private Sub Add_Click() Handles BtnAdd.Click
        AddSourceInteractive()
        RefreshSources()
    End Sub

    Private Sub Refresh_Click() Handles BtnRefresh.Click
        RefreshSources()
    End Sub

    Public Sub RefreshSources()
        PanSources.Children.Clear()
        Dim sources = LoadSources()
        If sources.Count = 0 Then
            PanSources.Children.Add(New TextBlock With {.Text = "尚未添加同步源。", .Opacity = 0.65, .Margin = New Thickness(0, 5, 0, 5)})
            CurrentSource = Nothing
            CurrentManifest = Nothing
            RefreshOverview()
            Return
        End If
        For Each source In sources.OfType(Of JObject)()
            Dim card As New Border With {
                .CornerRadius = New CornerRadius(7), .Padding = New Thickness(0),
                .Margin = New Thickness(0, 0, 0, 9)
            }
            card.SetResourceReference(Border.BackgroundProperty, "ColorBrushBackgroundTransparentSidebar")
            Dim root As New Grid
            root.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(4)})
            root.ColumnDefinitions.Add(New ColumnDefinition())
            root.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            Dim accent As New Border With {.CornerRadius = New CornerRadius(7, 0, 0, 7)}
            accent.SetResourceReference(Border.BackgroundProperty, "ColorBrush2")
            root.Children.Add(accent)
            Dim text As New StackPanel With {.Margin = New Thickness(17, 13, 16, 13)}
            Dim titleRow As New Grid
            titleRow.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            titleRow.ColumnDefinitions.Add(New ColumnDefinition())
            titleRow.Children.Add(New TextBlock With {
                .Text = If(source("instanceName")?.ToString(), "未命名服务器"),
                .FontSize = 16, .FontWeight = FontWeights.SemiBold
            })
            Dim statusText As New TextBlock With {
                .Text = "  ·  " & If(source("lastStatus")?.ToString(), "等待首次检查"),
                .Opacity = 0.48, .FontSize = 11, .VerticalAlignment = VerticalAlignment.Center,
                .TextTrimming = TextTrimming.CharacterEllipsis
            }
            Grid.SetColumn(statusText, 1)
            titleRow.Children.Add(statusText)
            text.Children.Add(titleRow)
            text.Children.Add(New TextBlock With {
                .Text = source("address")?.ToString(), .Opacity = 0.58,
                .Margin = New Thickness(0, 5, 15, 0), .TextTrimming = TextTrimming.CharacterEllipsis
            })
            text.Children.Add(New TextBlock With {
                .Text = "实例 " & source("instanceId")?.ToString() & "  ·  " &
                        If(source("instancePath")?.ToString(), "尚未绑定本地实例"),
                .Opacity = 0.42, .FontSize = 11, .Margin = New Thickness(0, 4, 15, 0),
                .TextTrimming = TextTrimming.CharacterEllipsis
            })
            Grid.SetColumn(text, 1)
            root.Children.Add(text)
            Dim buttons As New StackPanel With {
                .Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center,
                .Margin = New Thickness(10, 0, 15, 0)
            }
            Dim sourceKey = New JObject From {{"address", source("address")?.ToString()}, {"instanceId", source("instanceId")?.ToString()}}
            Dim preview As New MyButton With {
                .Text = "管理内容  →", .Padding = New Thickness(14, 7, 14, 7),
                .Margin = New Thickness(0, 0, 9, 0), .Tag = sourceKey, .ColorType = MyButton.ColorState.Highlight
            }
            AddHandler preview.Click, AddressOf Preview_Click
            Dim test As New MyIconButton With {
                .Logo = Logo.IconButtonInfo, .LogoScale = 0.95, .Height = 28, .Width = 28,
                .Margin = New Thickness(0, 0, 4, 0), .ToolTip = "测试连接", .Tag = sourceKey
            }
            AddHandler test.Click, AddressOf TestSource_Click
            Dim bind As New MyIconButton With {
                .Logo = Logo.IconButtonOpen, .LogoScale = 0.95, .Height = 28, .Width = 28,
                .Margin = New Thickness(0, 0, 4, 0), .ToolTip = "绑定本地实例", .Tag = sourceKey
            }
            AddHandler bind.Click, AddressOf BindSource_Click
            Dim reset As New MyIconButton With {
                .Logo = Logo.IconButtonRefresh, .LogoScale = 0.9, .Height = 28, .Width = 28,
                .Margin = New Thickness(0, 0, 4, 0), .ToolTip = "重置可选内容", .Tag = sourceKey
            }
            AddHandler reset.Click, AddressOf ResetOptional_Click
            Dim remove As New MyIconButton With {
                .Logo = Logo.IconButtonDelete, .LogoScale = 0.9, .Height = 28, .Width = 28,
                .ToolTip = "删除同步源", .Tag = sourceKey, .Theme = MyIconButton.Themes.Red
            }
            AddHandler remove.Click, AddressOf Remove_Click
            buttons.Children.Add(preview) : buttons.Children.Add(test) : buttons.Children.Add(bind)
            buttons.Children.Add(reset) : buttons.Children.Add(remove) : Grid.SetColumn(buttons, 2) : root.Children.Add(buttons)
            card.Child = root : PanSources.Children.Add(card)
        Next
        RefreshOverview()
    End Sub

    Private Sub RefreshOverview()
        Dim sources = LoadSources().OfType(Of JObject)().ToList()
        LabOverviewSources.Text = sources.Count & " 个"
        If CurrentSource Is Nothing Then
            LabOverviewCurrent.Text = "尚未选择服务器"
            Dim latest = sources.Where(Function(item) item("lastCheckedAt") IsNot Nothing).
                OrderByDescending(Function(item) item("lastCheckedAt").ToString()).FirstOrDefault()
            LabOverviewRelease.Text = If(latest?("lastReleaseId")?.ToString(), "尚未读取")
            LabOverviewStatus.Text = RuntimeSummary(latest)
            Return
        End If

        LabOverviewCurrent.Text = If(CurrentSource("instanceName")?.ToString(), "未命名服务器")
        LabOverviewRelease.Text = If(CurrentManifest?("releaseId")?.ToString(), If(CurrentSource("lastReleaseId")?.ToString(), "尚未读取"))
        Dim stored = sources.FirstOrDefault(
            Function(item) String.Equals(item("address")?.ToString(), CurrentSource("address")?.ToString(), StringComparison.OrdinalIgnoreCase) AndAlso
                           item("instanceId")?.ToString() = CurrentSource("instanceId")?.ToString())
        LabOverviewStatus.Text = RuntimeSummary(stored)
        If CurrentPlan IsNot Nothing Then
            Dim pending = DirectCast(CurrentPlan("added"), JArray).Count + DirectCast(CurrentPlan("updated"), JArray).Count +
                          DirectCast(CurrentPlan("deleted"), JArray).Count + DirectCast(CurrentPlan("conflicts"), JArray).Count
            LabOverviewStatus.Text &= $" · 本地已扫描，待处理 {pending} 项"
        End If
    End Sub

    Private Function RuntimeSummary(source As JObject) As String
        If source Is Nothing Then Return "尚无同步记录"
        Dim status = If(source("lastStatus")?.ToString(), "尚无同步记录")
        Dim timeText = source("lastCheckedAt")?.ToString()
        Dim checkedAt As Date
        If Date.TryParse(timeText, checkedAt) Then status &= " · " & checkedAt.ToString("yyyy-MM-dd HH:mm")
        If source("lastSyncedAt") IsNot Nothing Then
            Dim syncedAt As Date
            If Date.TryParse(source("lastSyncedAt").ToString(), syncedAt) Then status &= " · 最近同步 " & syncedAt.ToString("MM-dd HH:mm")
        End If
        Return status
    End Function

    Private Sub Preview_Click(sender As Object, e As EventArgs)
        Dim key = DirectCast(DirectCast(sender, MyButton).Tag, JObject)
        Dim address = key("address")?.ToString(), id = key("instanceId")?.ToString()
        Dim source = LoadSources().OfType(Of JObject)().FirstOrDefault(
            Function(item) String.Equals(item("address")?.ToString(), address, StringComparison.OrdinalIgnoreCase) AndAlso
                           item("instanceId")?.ToString() = id)
        If source Is Nothing Then Hint("同步源已不存在，请刷新列表！", HintType.Red) : Return
        TabMods.SetChecked(True, False, False)
        SelectIotaTab(1)
        LoadManifest(source)
    End Sub

    Private Sub LoadManifest(source As JObject)
        CurrentSource = DirectCast(source.DeepClone(), JObject)
        CurrentManifest = Nothing
        CurrentPlan = Nothing
        CurrentOptional = Nothing
        CurrentIgnored = Nothing
        CurrentModFiles.Clear()
        OptionalChecks.Clear()
        IgnoreChecks.Clear()
        PanMods.Children.Clear()
        PanIgnoreRules.Children.Clear()
        LabModsSummary.Text = "正在读取 " & source("instanceName")?.ToString() & " 的最新同步清单……"
        BtnModsRefresh.IsEnabled = False
        BtnOptionalAll.IsEnabled = False
        BtnOptionalNone.IsEnabled = False
        BtnDiffScan.IsEnabled = False
        BtnSyncNow.IsEnabled = False
        BtnGoDiff.IsEnabled = False
        RunInNewThread(
            Sub()
                Try
                    Dim manifest = GetSourceManifest(source)
                    RunInUi(Sub() RenderManifest(source, manifest))
                Catch ex As Exception
                    Dim message = ex.Message
                    RunInUi(
                        Sub()
                            LabModsSummary.Text = "读取同步清单失败：" & message
                            PanMods.Children.Clear()
                            BtnModsRefresh.IsEnabled = True
                            Hint("读取同步 Mod 清单失败：" & message, HintType.Red)
                        End Sub)
                End Try
            End Sub,
            "Iota Mod Preview")
    End Sub

    Private Sub RenderManifest(source As JObject, manifest As JObject)
        CurrentSource = DirectCast(source.DeepClone(), JObject)
        CurrentManifest = manifest
        CurrentOptional = TryCast(CurrentSource("optional"), JObject)
        If CurrentOptional Is Nothing Then CurrentOptional = New JObject()
        CurrentIgnored = TryCast(CurrentSource("ignored"), JArray)
        If CurrentIgnored Is Nothing Then CurrentIgnored = New JArray()
        OptionalChecks.Clear()
        IgnoreChecks.Clear()
        PanMods.Children.Clear()

        Dim files = DirectCast(manifest("files"), JArray).OfType(Of JObject)().ToList()
        Dim mods = files.Where(
            Function(file)
                Dim path = file("path")?.ToString()
                Return path IsNot Nothing AndAlso path.StartsWith("mods/", StringComparison.OrdinalIgnoreCase) AndAlso path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
            End Function).ToList()
        Dim required = mods.Where(Function(file) CInt(file("side")) = 0).ToList()
        Dim optionalFiles = mods.Where(Function(file) CInt(file("side")) = 1).ToList()
        Dim otherFiles = files.Count - mods.Count
        Dim release = manifest("releaseId")?.ToString()
        CurrentModFiles = mods
        LabModsSummary.Text = $"{manifest("instanceName")} · 正式版 {release} · {required.Count} 个必需 Mod · {optionalFiles.Count} 个可选 Mod" &
                              If(otherFiles > 0, $" · 另有 {otherFiles} 个配置或资源文件", "")
        RenderModList()
        RenderIgnoreRules(files)

        PersistOptionalSelections()
        UpdateSourceRuntime(CurrentSource, release, "清单已读取，可管理同步 Mod", False)
        CurrentSource("lastReleaseId") = release
        CurrentSource("lastStatus") = "清单已读取，可管理同步 Mod"
        CurrentSource("lastCheckedAt") = Date.Now.ToString("o")
        RefreshOverview()
        BtnModsRefresh.IsEnabled = True
        BtnOptionalAll.IsEnabled = optionalFiles.Count > 0
        BtnOptionalNone.IsEnabled = optionalFiles.Count > 0
        BtnDiffScan.IsEnabled = True
        BtnSyncNow.IsEnabled = Directory.Exists(ResolveInstancePath(CurrentSource))
        BtnGoDiff.IsEnabled = True
        RefreshBackups()
    End Sub

    Private Sub RenderModList()
        PanMods.Children.Clear()
        OptionalChecks.Clear()
        If CurrentManifest Is Nothing Then Return
        Dim query = TextModSearch.Text.Trim()
        Dim visible = CurrentModFiles.Where(
            Function(file)
                Return String.IsNullOrWhiteSpace(query) OrElse
                       file("path")?.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            End Function).ToList()
        Dim required = visible.Where(Function(file) CInt(file("side")) = 0).ToList()
        Dim optionalFiles = visible.Where(Function(file) CInt(file("side")) = 1).ToList()
        AddSectionTitle($"必需 Mod（{required.Count}）", "客户端必需 · 由服务器管理员纳入正式版，玩家不能关闭。")
        If required.Count = 0 Then AddEmptyRow("没有符合条件的必需 Mod。")
        For Each file In required.OrderBy(Function(item) item("path")?.ToString())
            AddRequiredRow(file)
        Next
        AddSectionTitle($"可选 Mod（{optionalFiles.Count}）", "客户端可选 · 勾选状态按服务器保存，可批量选择。")
        For Each file In optionalFiles.OrderBy(Function(item) item("path")?.ToString())
            Dim relative = file("path").ToString()
            If CurrentOptional(relative) Is Nothing Then CurrentOptional(relative) = True
            Dim check As New MyCheckBox With {
                .Text = "客户端可选  ·  " & FileLabel(file) & FileReason(file),
                .Checked = CBool(CurrentOptional(relative)),
                .Tag = relative,
                .Margin = New Thickness(4, 6, 4, 6),
                .ToolTip = relative
            }
            AddHandler check.Change, AddressOf Optional_Change
            OptionalChecks.Add(check)
            PanMods.Children.Add(check)
        Next
        If optionalFiles.Count = 0 Then AddEmptyRow("没有符合条件的可选 Mod。")
    End Sub

    Private Sub TextModSearch_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextModSearch.TextChanged
        If CurrentManifest IsNot Nothing Then RenderModList()
    End Sub

    Private Sub RenderIgnoreRules(files As List(Of JObject))
        PanIgnoreRules.Children.Clear()
        IgnoreChecks.Clear()
        Dim optionalNonMods = files.Where(
            Function(file)
                Dim path = file("path")?.ToString()
                Return CInt(file("side")) = 1 AndAlso
                       Not (path.StartsWith("mods/", StringComparison.OrdinalIgnoreCase) AndAlso path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            End Function).OrderBy(Function(file) file("path")?.ToString()).ToList()
        If optionalNonMods.Count = 0 Then
            PanIgnoreRules.Children.Add(New TextBlock With {.Text = "当前正式版没有管理员允许忽略的非 Mod 文件。", .Opacity = 0.6})
            Return
        End If
        Dim ignored = New HashSet(Of String)(CurrentIgnored.Values(Of String)(), StringComparer.OrdinalIgnoreCase)
        For Each file In optionalNonMods
            Dim relative = file("path").ToString()
            Dim check As New MyCheckBox With {
                .Text = "忽略  ·  " & relative & "  ·  " & FormatSize(If(file("size")?.ToObject(Of Long)(), 0)),
                .Checked = ignored.Contains(relative), .Tag = relative,
                .Margin = New Thickness(4, 6, 4, 6), .ToolTip = "勾选后不下载、不更新此可选文件"
            }
            AddHandler check.Change, AddressOf Ignore_Change
            IgnoreChecks.Add(check)
            PanIgnoreRules.Children.Add(check)
        Next
    End Sub

    Private Sub Ignore_Change(sender As Object, user As Boolean)
        If Not user OrElse CurrentSource Is Nothing Then Return
        CurrentIgnored = New JArray(IgnoreChecks.Where(Function(check) check.Checked).Select(Function(check) check.Tag.ToString()))
        SaveIgnoredSelections(CurrentSource("address").ToString(), CurrentSource("instanceId").ToString(), CurrentIgnored)
        CurrentSource("ignored") = CurrentIgnored.DeepClone()
        CurrentPlan = Nothing
        Hint("忽略规则已保存。", HintType.Green)
    End Sub

    Private Function FileReason(file As JObject) As String
        Dim reason = file("reason")?.ToString()
        If String.IsNullOrWhiteSpace(reason) Then reason = "服务器正式版声明"
        Return "  ·  " & reason
    End Function

    Private Sub AddSectionTitle(title As String, description As String)
        Dim panel As New StackPanel With {.Margin = New Thickness(0, 14, 0, 7)}
        panel.Children.Add(New TextBlock With {.Text = title, .FontSize = 15, .FontWeight = FontWeights.Bold})
        panel.Children.Add(New TextBlock With {.Text = description, .Opacity = 0.6, .Margin = New Thickness(0, 3, 0, 0), .TextWrapping = TextWrapping.Wrap})
        PanMods.Children.Add(panel)
    End Sub

    Private Sub AddRequiredRow(file As JObject)
        Dim border As New Border With {
            .CornerRadius = New CornerRadius(4),
            .BorderThickness = New Thickness(1),
            .BorderBrush = New SolidColorBrush(Color.FromArgb(25, 128, 128, 128)),
            .Padding = New Thickness(10, 8, 10, 8),
            .Margin = New Thickness(0, 0, 0, 6)
        }
        border.Child = New TextBlock With {.Text = "客户端必需  ·  " & FileLabel(file) & FileReason(file), .ToolTip = file("path")?.ToString(), .TextTrimming = TextTrimming.CharacterEllipsis}
        PanMods.Children.Add(border)
    End Sub

    Private Sub AddEmptyRow(text As String)
        PanMods.Children.Add(New TextBlock With {.Text = text, .Opacity = 0.55, .Margin = New Thickness(4, 5, 4, 8)})
    End Sub

    Private Function FileLabel(file As JObject) As String
        Dim path = file("path")?.ToString()
        Dim name = If(path?.Split("/"c).LastOrDefault(), path)
        Dim size = If(file("size")?.ToObject(Of Long)(), 0)
        Return $"{name}  ·  {FormatSize(size)}"
    End Function

    Private Function FormatSize(size As Long) As String
        If size < 1024 Then Return size & " B"
        If size < 1024L * 1024 Then Return Math.Round(size / 1024.0, 1) & " KB"
        Return Math.Round(size / 1024.0 / 1024.0, 1) & " MB"
    End Function

    Private Sub Optional_Change(sender As Object, user As Boolean)
        If Not user OrElse CurrentOptional Is Nothing Then Return
        Dim check = DirectCast(sender, MyCheckBox)
        CurrentOptional(check.Tag.ToString()) = check.Checked
        PersistOptionalSelections()
        Hint(If(check.Checked, "已选择安装可选 Mod。", "已取消可选 Mod。"), HintType.Green)
    End Sub

    Private Sub OptionalAll_Click() Handles BtnOptionalAll.Click
        SetAllOptional(True)
    End Sub

    Private Sub OptionalNone_Click() Handles BtnOptionalNone.Click
        SetAllOptional(False)
    End Sub

    Private Sub SetAllOptional(enabled As Boolean)
        If CurrentOptional Is Nothing Then Return
        For Each file In CurrentModFiles.Where(Function(item) CInt(item("side")) = 1)
            CurrentOptional(file("path").ToString()) = enabled
        Next
        PersistOptionalSelections()
        RenderModList()
        Hint(If(enabled, "已选择全部可选 Mod！", "已取消全部可选 Mod！"), HintType.Green)
    End Sub

    Private Sub ModsRefresh_Click() Handles BtnModsRefresh.Click
        If CurrentSource IsNot Nothing Then LoadManifest(CurrentSource)
    End Sub

    Private Sub PersistOptionalSelections()
        If CurrentSource Is Nothing OrElse CurrentOptional Is Nothing Then Return
        SaveOptionalSelections(CurrentSource("address").ToString(), CurrentSource("instanceId").ToString(), CurrentOptional)
        CurrentSource("optional") = CurrentOptional.DeepClone()
    End Sub

    Private Function FindSource(key As JObject) As JObject
        Return LoadSources().OfType(Of JObject)().FirstOrDefault(
            Function(item) String.Equals(item("address")?.ToString(), key("address")?.ToString(), StringComparison.OrdinalIgnoreCase) AndAlso
                           item("instanceId")?.ToString() = key("instanceId")?.ToString())
    End Function

    Private Sub TestSource_Click(sender As Object, e As EventArgs)
        Dim source = FindSource(DirectCast(DirectCast(sender, MyButton).Tag, JObject))
        If source Is Nothing Then Hint("同步源已不存在。", HintType.Red) : Return
        Hint("正在检查同步源……", HintType.Blue)
        RunInNewThread(
            Sub()
                Dim result = TestSourceConnection(source)
                RunInUi(
                    Sub()
                        If CBool(result("ok")) Then
                            MyMsgBox($"连接正常，延迟 {result("latency")} ms。" & vbCrLf &
                                     $"正式版：{result("releaseId")}，清单文件：{result("files")} 个。" & vbCrLf &
                                     result("message").ToString(), "同步源健康检查")
                        Else
                            MyMsgBox($"检查失败，耗时 {result("latency")} ms。" & vbCrLf & result("message").ToString(),
                                     "同步源健康检查", IsWarn:=True)
                        End If
                    End Sub)
            End Sub,
            "Iota Source Health")
    End Sub

    Private Sub BindSource_Click(sender As Object, e As EventArgs)
        Dim key = DirectCast(DirectCast(sender, MyButton).Tag, JObject)
        Dim source = FindSource(key)
        If source Is Nothing Then Hint("同步源已不存在。", HintType.Red) : Return
        Dim selected = Dialogs.SelectFolder("选择该同步源对应的 Minecraft 实例文件夹", False).FirstOrDefault()
        If String.IsNullOrWhiteSpace(selected) Then Return
        Try
            BindSourcePath(source("address").ToString(), source("instanceId").ToString(), selected)
            If CurrentSource IsNot Nothing AndAlso
               String.Equals(CurrentSource("address")?.ToString(), source("address")?.ToString(), StringComparison.OrdinalIgnoreCase) AndAlso
               CurrentSource("instanceId")?.ToString() = source("instanceId")?.ToString() Then
                CurrentSource("instancePath") = IO.Path.GetFullPath(selected).TrimEnd("\"c) & "\"
                RefreshBackups()
            End If
            RefreshSources()
            Hint("实例绑定已保存，今后不会因同名实例而串线。", HintType.Green)
        Catch ex As Exception
            MyMsgBox("无法绑定实例：" & ex.Message, "绑定失败", IsWarn:=True)
        End Try
    End Sub

    Private Sub DiffScan_Click() Handles BtnDiffScan.Click
        If CurrentSource Is Nothing OrElse CurrentManifest Is Nothing Then Return
        Dim source = DirectCast(CurrentSource.DeepClone(), JObject)
        Dim manifest = DirectCast(CurrentManifest.DeepClone(), JObject)
        BtnDiffScan.IsEnabled = False
        BtnSyncNow.IsEnabled = False
        LabDiffSummary.Text = "正在扫描本地文件和服务器清单……"
        PanDiff.Children.Clear()
        RunInNewThread(
            Sub()
                Try
                    Dim plan = BuildSyncPlan(source, manifest)
                    RunInUi(
                        Sub()
                            CurrentPlan = plan
                            RenderDiffPlan(plan)
                            RefreshOverview()
                            BtnDiffScan.IsEnabled = True
                            BtnSyncNow.IsEnabled = True
                        End Sub)
                Catch ex As Exception
                    RunInUi(
                        Sub()
                            LabDiffSummary.Text = "差异扫描失败：" & ex.Message
                            BtnDiffScan.IsEnabled = True
                            Hint("差异扫描失败：" & ex.Message, HintType.Red)
                        End Sub)
                End Try
            End Sub,
            "Iota Difference Scan")
    End Sub

    Private Sub GoDiff_Click() Handles BtnGoDiff.Click
        If CurrentSource Is Nothing OrElse CurrentManifest Is Nothing Then Return
        TabSync.SetChecked(True, False, False)
        SelectIotaTab(2)
        DiffScan_Click()
    End Sub

    Private Sub RenderDiffPlan(plan As JObject)
        PanDiff.Children.Clear()
        LabDiffSummary.Text = SyncPlanSummary(plan, False)
        AddDiffSection("新增文件", DirectCast(plan("added"), JArray), "将从服务器下载")
        AddDiffSection("更新文件", DirectCast(plan("updated"), JArray), "本地仍是上次同步版本")
        AddDiffSection("服务端已删除", DirectCast(plan("deleted"), JArray), "同步时移入恢复点后删除")
        AddDiffSection("本地修改冲突", DirectCast(plan("conflicts"), JArray), "同步时集中选择覆盖或全部保留")
        AddDiffSection("玩家自行添加", DirectCast(plan("extra"), JArray), "仅提示，始终保留")
    End Sub

    Private Sub AddDiffSection(title As String, items As JArray, description As String)
        Dim header As New TextBlock With {
            .Text = $"{title}（{items.Count}） · {description}", .FontWeight = FontWeights.Bold,
            .Margin = New Thickness(0, 10, 0, 5), .TextWrapping = TextWrapping.Wrap
        }
        PanDiff.Children.Add(header)
        If items.Count = 0 Then
            PanDiff.Children.Add(New TextBlock With {.Text = "无", .Opacity = 0.5, .Margin = New Thickness(5, 0, 0, 3)})
            Return
        End If
        For Each item In items.OfType(Of JObject)().Take(80)
            PanDiff.Children.Add(New TextBlock With {
                .Text = "• " & item("path")?.ToString() & If(item("reason") Is Nothing, "", " · " & item("reason").ToString()),
                .Opacity = 0.78, .Margin = New Thickness(5, 2, 0, 2), .TextTrimming = TextTrimming.CharacterEllipsis,
                .ToolTip = item("path")?.ToString()
            })
        Next
        If items.Count > 80 Then PanDiff.Children.Add(New TextBlock With {.Text = $"……另有 {items.Count - 80} 项", .Opacity = 0.55})
    End Sub

    Private Sub SyncNow_Click() Handles BtnSyncNow.Click
        If CurrentSource Is Nothing Then Return
        BtnSyncNow.IsEnabled = False
        BtnCancelSync.IsEnabled = True
        RunManualSync(CurrentSource)
    End Sub

    Private Sub CancelSync_Click() Handles BtnCancelSync.Click
        CancelIotaSync()
        BtnCancelSync.IsEnabled = False
    End Sub

    Private Sub RetrySync_Click() Handles BtnRetrySync.Click
        Dim source = GetLastFailedSource()
        If source Is Nothing Then Return
        BtnRetrySync.IsEnabled = False
        BtnCancelSync.IsEnabled = True
        RunManualSync(source)
    End Sub

    Private Sub OnSyncProgressChanged(status As String, progress As Double, canCancel As Boolean, failed As Boolean)
        RunInUi(
            Sub()
                LabTaskStatus.Text = status
                BarTaskProgress.Value = Math.Max(0, Math.Min(1, progress))
                BtnCancelSync.IsEnabled = canCancel
                BtnRetrySync.IsEnabled = failed
                If Not canCancel Then
                    BtnSyncNow.IsEnabled = CurrentSource IsNot Nothing
                    RefreshHistory()
                    RefreshBackups()
                    If CurrentSource IsNot Nothing AndAlso CurrentManifest IsNot Nothing Then
                        BtnDiffScan.IsEnabled = True
                    End If
                End If
            End Sub)
    End Sub

    Private Sub HistoryRefresh_Click() Handles BtnHistoryRefresh.Click
        RefreshHistory()
    End Sub

    Private Sub RefreshHistory()
        PanHistory.Children.Clear()
        Dim history = LoadIotaHistory().OfType(Of JObject)().Take(30).ToList()
        If history.Count = 0 Then
            PanHistory.Children.Add(New TextBlock With {.Text = "尚无同步历史。", .Opacity = 0.6})
            Return
        End If
        For Each item In history
            Dim time As Date
            Dim timeText = item("time")?.ToString()
            If Date.TryParse(timeText, time) Then timeText = time.ToString("yyyy-MM-dd HH:mm:ss")
            PanHistory.Children.Add(New TextBlock With {
                .Text = $"{timeText} · {item("instanceName")} · {item("result")} · {item("releaseId")} · {item("fileCount")} 个文件 · {FormatIotaSize(If(item("bytes")?.ToObject(Of Long)(), 0))}" &
                        If(item("message") Is Nothing, "", " · " & item("message").ToString()),
                .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 3, 0, 6)
            })
        Next
    End Sub

    Private Sub ExportDiagnostic_Click() Handles BtnExportDiagnostic.Click
        Dim path = Dialogs.SaveFile("保存 PCL IO 诊断报告", "PCL-IO-诊断-" & Date.Now.ToString("yyyyMMdd-HHmmss") & ".txt", filter:={("txt", "文本诊断报告")})
        If String.IsNullOrWhiteSpace(path) Then Return
        Try
            File.WriteAllText(path, BuildDiagnosticReport(CurrentSource), Text.Encoding.UTF8)
            Hint("诊断报告已导出，完整同步码未写入报告。", HintType.Green)
        Catch ex As Exception
            MyMsgBox("导出诊断报告失败：" & ex.Message, "导出失败", IsWarn:=True)
        End Try
    End Sub

    Private Sub RefreshBackups()
        PanBackups.Children.Clear()
        If CurrentSource Is Nothing Then
            LabBackupSummary.Text = "选择已绑定的同步源后显示该实例的恢复点。"
            Return
        End If
        Dim backups = LoadBackupPoints(CurrentSource).OfType(Of JObject)().Take(20).ToList()
        LabBackupSummary.Text = $"实例 {CurrentSource("instanceName")} · 共 {backups.Count} 个近期恢复点"
        If backups.Count = 0 Then
            PanBackups.Children.Add(New TextBlock With {.Text = "尚无恢复点；首次发生文件变更时会自动创建。", .Opacity = 0.6})
            Return
        End If
        For Each backup In backups
            Dim row As New Grid With {.Margin = New Thickness(0, 0, 0, 8)}
            row.ColumnDefinitions.Add(New ColumnDefinition())
            row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            Dim time As Date, timeText = backup("createdAt")?.ToString()
            If Date.TryParse(timeText, time) Then timeText = time.ToString("yyyy-MM-dd HH:mm:ss")
            row.Children.Add(New TextBlock With {
                .Text = $"{timeText} · {backup("fromRelease")} → {backup("toRelease")} · {DirectCast(backup("entries"), JArray).Count} 项",
                .VerticalAlignment = VerticalAlignment.Center, .TextWrapping = TextWrapping.Wrap
            })
            Dim restore As New MyButton With {.Text = "一键恢复", .Padding = New Thickness(12, 6, 12, 6), .Tag = backup("metadataPath")?.ToString()}
            AddHandler restore.Click, AddressOf RestoreBackup_Click
            Grid.SetColumn(restore, 1)
            row.Children.Add(restore)
            PanBackups.Children.Add(row)
        Next
    End Sub

    Private Sub RestoreBackup_Click(sender As Object, e As EventArgs)
        Dim metadataPath = DirectCast(sender, MyButton).Tag?.ToString()
        If MyMsgBox("将把实例恢复到此次同步前的状态。当前同名文件可能被覆盖，是否继续？",
                    "一键恢复", "确认恢复", "取消", IsWarn:=True) <> 1 Then Return
        Try
            RestoreBackupPoint(metadataPath)
            CurrentPlan = Nothing
            RefreshHistory()
            RefreshBackups()
            Hint("恢复完成。建议重新扫描差异后再启动游戏。", HintType.Green)
        Catch ex As Exception
            MyMsgBox("恢复失败：" & ex.Message, "恢复失败", IsWarn:=True)
        End Try
    End Sub

    Private Sub ResetOptional_Click(sender As Object, e As EventArgs)
        Dim sources = LoadSources(), key = DirectCast(DirectCast(sender, MyButton).Tag, JObject)
        Dim address = key("address")?.ToString(), id = key("instanceId")?.ToString()
        For Each source In sources.OfType(Of JObject)().Where(Function(x) String.Equals(x("address")?.ToString(), address, StringComparison.OrdinalIgnoreCase) AndAlso x("instanceId")?.ToString() = id)
            source.Remove("optional")
        Next
        SaveSources(sources)
        If CurrentSource IsNot Nothing AndAlso String.Equals(CurrentSource("address")?.ToString(), address, StringComparison.OrdinalIgnoreCase) AndAlso CurrentSource("instanceId")?.ToString() = id Then LoadManifest(source:=sources.OfType(Of JObject)().First(Function(x) String.Equals(x("address")?.ToString(), address, StringComparison.OrdinalIgnoreCase) AndAlso x("instanceId")?.ToString() = id))
        Hint("已重置可选内容选择！", HintType.Green)
    End Sub

    Private Sub Remove_Click(sender As Object, e As EventArgs)
        Dim key = DirectCast(DirectCast(sender, MyButton).Tag, JObject)
        Dim address = key("address")?.ToString(), id = key("instanceId")?.ToString()
        If MyMsgBox("确定删除这个同步源吗？本地游戏文件不会被删除。", "删除同步源", "确定", "取消", IsWarn:=True) <> 1 Then Return
        RemoveSource(address, id)
        If CurrentSource IsNot Nothing AndAlso String.Equals(CurrentSource("address")?.ToString(), address, StringComparison.OrdinalIgnoreCase) AndAlso CurrentSource("instanceId")?.ToString() = id Then
            CurrentSource = Nothing : CurrentManifest = Nothing : CurrentPlan = Nothing : CurrentOptional = Nothing : CurrentIgnored = Nothing
            CurrentModFiles.Clear() : OptionalChecks.Clear() : IgnoreChecks.Clear()
            PanMods.Children.Clear() : PanIgnoreRules.Children.Clear() : PanDiff.Children.Clear() : PanBackups.Children.Clear()
            LabModsSummary.Text = "点击同步源卡片中的管理 Mod 按钮，读取最新正式版清单。"
            LabDiffSummary.Text = "选择同步源后可扫描本地实例与服务器正式版的差异。"
            LabBackupSummary.Text = "选择已绑定的同步源后显示该实例的恢复点。"
            BtnModsRefresh.IsEnabled = False : BtnOptionalAll.IsEnabled = False : BtnOptionalNone.IsEnabled = False
            BtnDiffScan.IsEnabled = False : BtnSyncNow.IsEnabled = False : BtnGoDiff.IsEnabled = False
        End If
        RefreshSources() : Hint("同步源已删除！", HintType.Green)
    End Sub
End Class
