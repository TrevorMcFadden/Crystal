Imports Windows.Graphics.Imaging
Imports Windows.Media.Ocr
Imports Windows.Storage
Imports Windows.Storage.Pickers
Imports Windows.Storage.Streams
Imports Windows.System

Public NotInheritable Class CrystalFilePage
    Inherits Page
    Private BMP As SoftwareBitmap
    Private WB As List(Of WordOverlay) = New List(Of WordOverlay)()
    Public Sub New()
        InitializeComponent()
        NavigationCacheMode = NavigationCacheMode.Enabled
    End Sub
    Private Async Function LoadImageAsync(file As StorageFile) As Task
        Using stream = Await file.OpenAsync(FileAccessMode.Read)
            Dim DCDR = Await BitmapDecoder.CreateAsync(stream)
            BMP = Await DCDR.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
            Dim IMS = New WriteableBitmap(BMP.PixelWidth, BMP.PixelHeight)
            BMP.CopyToBuffer(IMS.PixelBuffer)
            PreviewImage.Source = IMS
        End Using
    End Function
    Private Async Function LoadSampleImageAsync() As Task
        Dim file = Await Package.Current.InstalledLocation.GetFileAsync("Assets\\Graphic Design - 13 Reasons.png")
        Await LoadImageAsync(file)
    End Function
    Private Sub ClearResults()
        TextOverlay.RenderTransform = Nothing
        ExtractedTextBox.Text = String.Empty
        TextOverlay.Children.Clear()
        WB.Clear()
    End Sub
    Private Sub UpdateWordBoxTransform()
        Dim ST As ScaleTransform = New ScaleTransform With {
                .CenterX = 0,
                .CenterY = 0,
                .ScaleX = PreviewImage.ActualWidth / BMP.PixelWidth,
                .ScaleY = PreviewImage.ActualHeight / BMP.PixelHeight
            }
        For Each item In WB
            item.Transform(ST)
        Next
    End Sub
    Private Async Sub ExtractTextButton_Click(sender As Object, e As RoutedEventArgs) Handles ExtractTextButton.Click
        Dim OE As OcrEngine
        ClearResults()
        If BMP.PixelWidth > OcrEngine.MaxImageDimension OrElse BMP.PixelHeight > OcrEngine.MaxImageDimension Then
            Dim BD As ContentDialog = New ContentDialog() With {
                .Title = "Sizing Error",
                .Content = String.Format("Whoa! The bitmap dimensions ({0}x{1}) are way too big for OCR.", BMP.PixelWidth, BMP.PixelHeight) & "The maximum image dimension allowed is " + OcrEngine.MaxImageDimension & ".",
                .CloseButtonText = "Close"
            }
            Await BD.ShowAsync()
            Return
        End If
        OE = OcrEngine.TryCreateFromUserProfileLanguages()
        If OE IsNot Nothing Then
            Dim OCRR = Await OE.RecognizeAsync(BMP)
            ExtractedTextBox.Text = OCRR.Text
            If OCRR.TextAngle IsNot Nothing Then
                TextOverlay.RenderTransform = New RotateTransform With {
                        .Angle = OCRR.TextAngle,
                        .CenterX = PreviewImage.ActualWidth / 2,
                        .CenterY = PreviewImage.ActualHeight / 2
                    }
            End If
            For Each line In OCRR.Lines
                Dim lineRect As Rect = Rect.Empty
                For Each word In line.Words
                    lineRect.Union(word.BoundingRect)
                Next
                Dim isVerticalLine As Boolean = lineRect.Height > lineRect.Width
                Dim style = If(isVerticalLine, HighlightedWordBoxVerticalLineStyle, HighlightedWordBoxHorizontalLineStyle)
                For Each word In line.Words
                    Dim WBO As WordOverlay = New WordOverlay(word)
                    WB.Add(WBO)
                    TextOverlay.Children.Add(WBO.CreateBorder(style))
                Next
            Next
            UpdateWordBoxTransform()
            Dim OD As ContentDialog = New ContentDialog() With {
                .Title = "OCR Success",
                .Content = "Success! The image is OCRed in the " & OE.RecognizerLanguage.DisplayName & " language.",
                .CloseButtonText = "Ok"
            }
            Await OD.ShowAsync()
            SaveAsDocumentButton.IsEnabled = True
        Else
            Dim SLD As ContentDialog = New ContentDialog() With {
                .Title = "OCR Erorr",
                .Content = "Sorry, the selected language is not available. Please hang up the phone and try again.",
                .CloseButtonText = "Close"
            }
            Await SLD.ShowAsync()
            SaveAsDocumentButton.IsEnabled = False
        End If
    End Sub
    Private Async Sub LoadDefaultButton_Click(sender As Object, e As RoutedEventArgs) Handles LoadDefaultImageButton.Click
        ExtractTextButton.IsEnabled = True
        ClearResults()
        Await LoadSampleImageAsync()
        Dim LSID As ContentDialog = New ContentDialog() With {
                .Title = "Crystal Sample Image",
                .Content = "The sample image was loaded.",
                .CloseButtonText = "Ok"
            }
        Await LSID.ShowAsync()
    End Sub
    Private Async Sub UploadButton_Click(sender As Object, e As RoutedEventArgs) Handles UploadButton.Click
        Dim open As FileOpenPicker = New FileOpenPicker()
        open.SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        open.FileTypeFilter.Add(".jpg")
        open.FileTypeFilter.Add(".gif")
        open.FileTypeFilter.Add(".png")
        Dim file As StorageFile = Await open.PickSingleFileAsync()
        If file Is Nothing Then
            Dim UED As ContentDialog = New ContentDialog() With {
                .Title = "Open File Error",
                .Content = "Sorry, the selected file could not be opened. Please hang up and try again.",
                .CloseButtonText = "Close"
            }
            Await UED.ShowAsync()
        End If
        If file IsNot Nothing Then
            ExtractTextButton.IsEnabled = True
            ClearResults()
            Await LoadImageAsync(file)
            Dim USD As ContentDialog = New ContentDialog() With {
                .Title = "Upload File Success",
                .Content = String.Format("Success! Crystal was able to load the file {0} ({1}x{2}).", file.Name, BMP.PixelWidth, BMP.PixelHeight),
                .CloseButtonText = "Ok"
            }
            Await USD.ShowAsync()
        End If
    End Sub
    Private Sub PreviewImage_SizeChanged(sender As Object, e As SizeChangedEventArgs) Handles PreviewImage.SizeChanged
        UpdateWordBoxTransform()
        Dim rotate = TryCast(TextOverlay.RenderTransform, RotateTransform)
        If rotate IsNot Nothing Then
            rotate.CenterX = PreviewImage.ActualWidth / 2
            rotate.CenterY = PreviewImage.ActualHeight / 2
        End If
    End Sub
    Private Sub GoToCameraPageButton_Click(sender As Object, e As RoutedEventArgs) Handles GoToCameraPage.Click
        Frame.Navigate(GetType(CrystalCameraPage))
    End Sub
    Private Async Sub SaveAsDocumentButton_Click(sender As Object, e As RoutedEventArgs) Handles SaveAsDocumentButton.Click
        Dim savePicker As FileSavePicker = New FileSavePicker()
        Dim REB As New RichEditBox
        REB.Document.SetText(Windows.UI.Text.TextSetOptions.FormatRtf, ExtractedTextBox.Text)
        savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        savePicker.FileTypeChoices.Add("Rich Text Format (.rtf)", New List(Of String)() From {
            ".rtf"
        })
        savePicker.SuggestedFileName = "CrystalOCR"
        Dim file As StorageFile = Await savePicker.PickSaveFileAsync()
        If file IsNot Nothing Then
            CachedFileManager.DeferUpdates(file)
            Dim randAccStream As IRandomAccessStream = Await file.OpenAsync(FileAccessMode.ReadWrite)
            REB.Document.SaveToStream(Windows.UI.Text.TextGetOptions.FormatRtf, randAccStream)
            Dim status As Provider.FileUpdateStatus = Await CachedFileManager.CompleteUpdatesAsync(file)
            Dim SFD As ContentDialog = New ContentDialog() With {
                .Title = "File Saved",
                .Content = String.Format("Success! Crystal was able to save the file {0}.", file.Name),
                .CloseButtonText = "Ok"
            }
            Await SFD.ShowAsync()
            If status <> Provider.FileUpdateStatus.Complete Then
                Dim ERB As ContentDialog = New ContentDialog() With {
                .Title = "File Save Error",
                .Content = "Sorry, the file could not be saved. Please hang up the phone and try again.",
                .CloseButtonText = "Close"
            }
                Await ERB.ShowAsync()
            End If
        End If
    End Sub
    Private Async Sub Me_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Dim u As IReadOnlyList(Of User) = Await User.FindAllAsync()
        Dim current = u.Where(Function(p) p.AuthenticationStatus = UserAuthenticationStatus.LocallyAuthenticated AndAlso p.Type = UserType.LocalUser).FirstOrDefault()
        Dim streamReference As IRandomAccessStreamReference = Await current.GetPictureAsync(UserPictureSize.Size64x64)
        If streamReference IsNot Nothing Then
            Dim stream As IRandomAccessStream = Await streamReference.OpenReadAsync()
            Dim bitmapImage As BitmapImage = New BitmapImage()
            bitmapImage.SetSource(stream)
            ProfilePhoto.Source = bitmapImage
        End If
    End Sub
End Class