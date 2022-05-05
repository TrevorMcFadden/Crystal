Imports Windows.Devices.Enumeration
Imports Windows.Globalization
Imports Windows.Graphics.Imaging
Imports Windows.Media
Imports Windows.Media.Capture
Imports Windows.Media.MediaProperties
Imports Windows.Media.Ocr
Imports Windows.System.Display
Imports Windows.UI.Core
Imports System.Runtime.InteropServices
Imports Windows.Storage.Streams
Imports Windows.System

Public NotInheritable Class CrystalCameraPage
    Inherits Page
    Private OL As Language = New Language("en")
    Private WB As List(Of WordOverlay) = New List(Of WordOverlay)()
    Private ReadOnly DI As DisplayInformation = DisplayInformation.GetForCurrentView()
    Private Shared ReadOnly RK As Guid = New Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1")
    Private ReadOnly DR As DisplayRequest = New DisplayRequest()
    Private MC As MediaCapture
    Private II As Boolean = False
    Private IP As Boolean = False
    Private MP As Boolean = False
    Private EC As Boolean = False
    Public Sub New()
        InitializeComponent()
        NavigationCacheMode = NavigationCacheMode.Enabled
    End Sub
    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        AddHandler DI.OrientationChanged, AddressOf DisplayInformation_OrientationChanged
        AddHandler Application.Current.Suspending, AddressOf Application_Suspending
        AddHandler Application.Current.Resuming, AddressOf Application_Resuming
        If Not OcrEngine.IsLanguageSupported(OL) Then
            Dim OLD As ContentDialog = New ContentDialog() With {
                .Title = "Unsupported OCR Language",
                .Content = "Sorry, " & OL.DisplayName & " is not supported.",
                .CloseButtonText = "Close"
            }
            Await OLD.ShowAsync()
            Return
        End If
        Await StartCameraAsync()
    End Sub
    Protected Overrides Async Sub OnNavigatingFrom(e As NavigatingCancelEventArgs)
        AddHandler DI.OrientationChanged, AddressOf DisplayInformation_OrientationChanged
        AddHandler Application.Current.Suspending, AddressOf Application_Suspending
        AddHandler Application.Current.Resuming, AddressOf Application_Resuming
        Await CleanupCameraAsync()
    End Sub
    Private Async Sub Application_Suspending(sender As Object, e As SuspendingEventArgs)
        Dim deferral = e.SuspendingOperation.GetDeferral()
        Await CleanupCameraAsync()
        AddHandler DI.OrientationChanged, AddressOf DisplayInformation_OrientationChanged
        deferral.Complete()
    End Sub
    Private Async Sub Application_Resuming(sender As Object, o As Object)
        AddHandler DI.OrientationChanged, AddressOf DisplayInformation_OrientationChanged
        Await StartCameraAsync()
    End Sub
    Private Async Sub DisplayInformation_OrientationChanged(sender As DisplayInformation, args As Object)
        If IP Then
            Await SetPreviewRotationAsync()
        End If
    End Sub
    Private Async Sub ExtractTextButton_Click(sender As Object, e As RoutedEventArgs) Handles ExtractTextButton.Click
        Dim previewProperties = TryCast(MC.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview), VideoEncodingProperties)
        Dim videoFrameWidth As Integer = previewProperties.Width
        Dim videoFrameHeight As Integer = previewProperties.Height
        If Not EC AndAlso (DI.CurrentOrientation = DisplayOrientations.Portrait OrElse DI.CurrentOrientation = DisplayOrientations.PortraitFlipped) Then
            videoFrameWidth = CInt(previewProperties.Height)
            videoFrameHeight = CInt(previewProperties.Width)
        End If
        Dim videoFrame = New VideoFrame(BitmapPixelFormat.Bgra8, videoFrameWidth, videoFrameHeight)
        Using currentFrame = Await MC.GetPreviewFrameAsync(videoFrame)
            Dim bitmap As SoftwareBitmap = currentFrame.SoftwareBitmap
            Dim ocrEngine As OcrEngine = OcrEngine.TryCreateFromLanguage(OL)
            If ocrEngine Is Nothing Then
                Dim OLD As ContentDialog = New ContentDialog() With {
                .Title = "Unsupported OCR Language",
                .Content = "Sorry, " & OL.DisplayName & " is not supported.",
                .CloseButtonText = "Close"
            }
                Await OLD.ShowAsync()
                Return
            End If
            Dim imgSource = New WriteableBitmap(bitmap.PixelWidth, bitmap.PixelHeight)
            bitmap.CopyToBuffer(imgSource.PixelBuffer)
            PreviewImage.Source = imgSource
            Dim ocrResult = Await ocrEngine.RecognizeAsync(bitmap)
            Dim scaleTrasform = New ScaleTransform With {
                    .CenterX = 0,
                    .CenterY = 0,
                    .ScaleX = PreviewControl.ActualWidth / bitmap.PixelWidth,
                    .ScaleY = PreviewControl.ActualHeight / bitmap.PixelHeight
                }
            If ocrResult.TextAngle IsNot Nothing Then
                TextOverlay.RenderTransform = New RotateTransform With {
                        .Angle = ocrResult.TextAngle,
                        .CenterX = PreviewImage.ActualWidth / 2,
                        .CenterY = PreviewImage.ActualHeight / 2
                    }
            End If
            For Each line In ocrResult.Lines
                For Each word In line.Words
                    Dim wordBoxOverlay As WordOverlay = New WordOverlay(word)
                    WB.Add(wordBoxOverlay)
                    Dim textBlock = New TextBlock() With {
                            .Text = word.Text,
                            .Style = ExtractedWordTextStyle
                        }
                    TextOverlay.Children.Add(wordBoxOverlay.CreateBorder(HighlightedWordBoxHorizontalLineStyle, textBlock))
                Next
            Next
            Dim IMPD As ContentDialog = New ContentDialog() With {
                .Title = "OCR Success",
                .Content = "Success! The image was processed in the " & ocrEngine.RecognizerLanguage.DisplayName & " language.",
                .CloseButtonText = "Ok"
            }
            Await IMPD.ShowAsync()
        End Using
        UpdateWordBoxTransform()
        PreviewControl.Visibility = Visibility.Collapsed
        Image.Visibility = Visibility.Visible
        ExtractTextButton.IsEnabled = False
        TriggerCameraButton.IsEnabled = True
    End Sub
    Private Async Sub TriggerCameraButton_Click(sender As Object, e As RoutedEventArgs) Handles TriggerCameraButton.Click
        Await StartCameraAsync()
    End Sub
    Private Sub UpdateWordBoxTransform()
        Dim bitmap As WriteableBitmap = TryCast(PreviewImage.Source, WriteableBitmap)
        If bitmap IsNot Nothing Then
            Dim scaleTransform As ScaleTransform = New ScaleTransform With {
                    .CenterX = 0,
                    .CenterY = 0,
                    .ScaleX = PreviewImage.ActualWidth / bitmap.PixelWidth,
                    .ScaleY = PreviewImage.ActualHeight / bitmap.PixelHeight
                }
            For Each item In WB
                item.Transform(scaleTransform)
            Next
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
    Private Async Function StartCameraAsync() As Task
        If Not II Then
            Await InitializeCameraAsync()
        End If
        If II Then
            TextOverlay.Children.Clear()
            WB.Clear()
            PreviewImage.Source = Nothing
            PreviewControl.Visibility = Visibility.Visible
            Image.Visibility = Visibility.Collapsed
            ExtractTextButton.IsEnabled = True
            TriggerCameraButton.IsEnabled = False
        End If
    End Function
    Private Async Function InitializeCameraAsync() As Task
        If MC Is Nothing Then
            Dim cameraDevice = Await FindCameraDeviceByPanelAsync(Panel.Back)
            If cameraDevice Is Nothing Then
                Dim NCFD As ContentDialog = New ContentDialog() With {
                .Title = "OCR Camera Error",
                .Content = "Sorry, no camera was found. Please hang up the phone and try again.",
                .CloseButtonText = "Close"
            }
                Await NCFD.ShowAsync()
                Return
            End If
            MC = New MediaCapture()
            AddHandler MC.Failed, AddressOf MediaCapture_Failed
            Dim settings = New MediaCaptureInitializationSettings With {
                    .VideoDeviceId = cameraDevice.Id
                }
            Try
                Await MC.InitializeAsync(settings)
                II = True
            Catch __unusedUnauthorizedAccessException1__ As UnauthorizedAccessException
                Debug.WriteLine("Denied access to the camera.")
            Catch ex As Exception
                Debug.WriteLine("Exception when init MediaCapture. " & ex.Message)
            End Try
            If II Then
                If cameraDevice.EnclosureLocation Is Nothing OrElse cameraDevice.EnclosureLocation.Panel = Windows.Devices.Enumeration.Panel.Unknown Then
                    EC = True
                Else
                    EC = False
                    MP = (cameraDevice.EnclosureLocation.Panel = Windows.Devices.Enumeration.Panel.Front)
                End If
                Await StartPreviewAsync()
            End If
        End If
    End Function
    Private Async Function StartPreviewAsync() As Task
        DR.RequestActive()
        PreviewControl.Source = MC
        PreviewControl.FlowDirection = If(MP, FlowDirection.RightToLeft, FlowDirection.LeftToRight)
        Try
            Await MC.StartPreviewAsync()
            IP = True
        Catch ex As Exception
            Debug.WriteLine("Exception starting preview." & ex.Message)
        End Try
        If IP Then
            Await SetPreviewRotationAsync()
        End If
    End Function
    Private Async Function SetPreviewRotationAsync() As Task
        If EC Then Return
        Dim rotationDegrees As Integer
        Dim sourceRotation As VideoRotation
        CalculatePreviewRotation(sourceRotation, rotationDegrees)
        MC.SetPreviewRotation(sourceRotation)
        Dim props = MC.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview)
        props.Properties.Add(RK, rotationDegrees)
        Await MC.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, Nothing)
    End Function
    Private Async Function StopPreviewAsync() As Task
        Try
            IP = False
            Await MC.StopPreviewAsync()
        Catch ex As Exception
            Debug.WriteLine("Exception stopping preview. " & ex.Message)
        End Try
        Await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, Function()
                                                                     PreviewControl.Source = Nothing
                                                                     DR.RequestRelease()
                                                                 End Function)
    End Function
    Private Async Function CleanupCameraAsync() As Task
        If II Then
            If IP Then
                Await StopPreviewAsync()
            End If
            II = False
        End If
        If MC IsNot Nothing Then
            AddHandler MC.Failed, AddressOf MediaCapture_Failed
            MC.Dispose()
            MC = Nothing
        End If
    End Function
    Private Shared Async Function FindCameraDeviceByPanelAsync(ByVal desiredPanel As Windows.Devices.Enumeration.Panel) As Task(Of DeviceInformation)
        Dim allVideoDevices = Await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture)
        Dim desiredDevice As DeviceInformation = allVideoDevices.FirstOrDefault(Function(x) x.EnclosureLocation IsNot Nothing AndAlso x.EnclosureLocation.Panel = desiredPanel)
        Return If(desiredDevice, allVideoDevices.FirstOrDefault())
    End Function
    Private Sub CalculatePreviewRotation(<Out> ByRef sourceRotation As VideoRotation, <Out> ByRef rotationDegrees As Integer)
        Select Case DI.CurrentOrientation
            Case DisplayOrientations.Portrait
                If MP Then
                    rotationDegrees = 270
                    sourceRotation = VideoRotation.Clockwise270Degrees
                Else
                    rotationDegrees = 90
                    sourceRotation = VideoRotation.Clockwise90Degrees
                End If
            Case DisplayOrientations.LandscapeFlipped
                rotationDegrees = 180
                sourceRotation = VideoRotation.Clockwise180Degrees
            Case DisplayOrientations.PortraitFlipped
                If MP Then
                    rotationDegrees = 90
                    sourceRotation = VideoRotation.Clockwise90Degrees
                Else
                    rotationDegrees = 270
                    sourceRotation = VideoRotation.Clockwise270Degrees
                End If
            Case Else
                rotationDegrees = 0
                sourceRotation = VideoRotation.None
        End Select
    End Sub
    Private Async Sub MediaCapture_Failed(sender As MediaCapture, errorEventArgs As MediaCaptureFailedEventArgs)
        Await CleanupCameraAsync()
        Await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, Function()
                                                                     ExtractTextButton.IsEnabled = False
                                                                     TriggerCameraButton.IsEnabled = True
                                                                     Debug.WriteLine("MediaCapture Failed. " & errorEventArgs.Message)
                                                                 End Function)
    End Sub
    Private Sub BackToFileButton_Click(sender As Object, e As RoutedEventArgs) Handles BackToFileButton.Click
        Frame.Navigate(GetType(CrystalFilePage))
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