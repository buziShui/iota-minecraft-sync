Imports Newtonsoft.Json.Linq

Public Class PageSetupIota
    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PanBack.ScrollToHome()
        RefreshSources()
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
            Return
        End If
        For Each source In sources.OfType(Of JObject)()
            Dim card As New Border With {.CornerRadius = New CornerRadius(6), .BorderThickness = New Thickness(1), .BorderBrush = New SolidColorBrush(Color.FromArgb(35, 128, 128, 128)), .Padding = New Thickness(15), .Margin = New Thickness(0, 0, 0, 10)}
            Dim root As New Grid
            root.ColumnDefinitions.Add(New ColumnDefinition())
            root.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
            Dim text As New StackPanel
            text.Children.Add(New TextBlock With {.Text = source("instanceName")?.ToString(), .FontSize = 16, .FontWeight = FontWeights.Bold})
            text.Children.Add(New TextBlock With {.Text = source("address")?.ToString(), .Opacity = 0.65, .Margin = New Thickness(0, 5, 15, 0), .TextWrapping = TextWrapping.Wrap})
            text.Children.Add(New TextBlock With {.Text = "绑定实例 ID：" & source("instanceId")?.ToString() & "　同步码：已安全保存", .Opacity = 0.55, .FontSize = 11, .Margin = New Thickness(0, 4, 15, 0)})
            root.Children.Add(text)
            Dim buttons As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
            Dim sourceKey = New JObject From {{"address", source("address")?.ToString()}, {"instanceId", source("instanceId")?.ToString()}}
            Dim reset As New MyButton With {.Text = "重置可选项", .Padding = New Thickness(12, 6, 12, 6), .Margin = New Thickness(0, 0, 8, 0), .Tag = sourceKey}
            AddHandler reset.Click, AddressOf ResetOptional_Click
            Dim remove As New MyButton With {.Text = "删除", .Padding = New Thickness(12, 6, 12, 6), .Tag = sourceKey}
            AddHandler remove.Click, AddressOf Remove_Click
            buttons.Children.Add(reset) : buttons.Children.Add(remove) : Grid.SetColumn(buttons, 1) : root.Children.Add(buttons)
            card.Child = root : PanSources.Children.Add(card)
        Next
    End Sub

    Private Sub ResetOptional_Click(sender As Object, e As EventArgs)
        Dim sources = LoadSources(), key = DirectCast(DirectCast(sender, MyButton).Tag, JObject)
        Dim address = key("address")?.ToString(), id = key("instanceId")?.ToString()
        For Each source In sources.OfType(Of JObject)().Where(Function(x) String.Equals(x("address")?.ToString(), address, StringComparison.OrdinalIgnoreCase) AndAlso x("instanceId")?.ToString() = id)
            source.Remove("optional")
        Next
        SaveSources(sources) : Hint("已重置可选内容选择！", HintType.Green)
    End Sub

    Private Sub Remove_Click(sender As Object, e As EventArgs)
        Dim key = DirectCast(DirectCast(sender, MyButton).Tag, JObject)
        Dim address = key("address")?.ToString(), id = key("instanceId")?.ToString()
        If MyMsgBox("确定删除这个同步源吗？本地游戏文件不会被删除。", "删除同步源", "确定", "取消", IsWarn:=True) <> 1 Then Return
        RemoveSource(address, id) : RefreshSources() : Hint("同步源已删除！", HintType.Green)
    End Sub
End Class
