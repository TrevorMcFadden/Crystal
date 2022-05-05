Imports Windows.Media.Ocr

Public Class WordOverlay
    Implements INotifyPropertyChanged
    Private Word As OcrWord
    Private WordBoundingRect As Rect
    Public ReadOnly Property WordPosition As Thickness
        Get
            Return New Thickness(WordBoundingRect.Left, WordBoundingRect.Top, 0, 0)
        End Get
    End Property
    Public ReadOnly Property WordWidth As Double
        Get
            Return WordBoundingRect.Width
        End Get
    End Property
    Public ReadOnly Property WordHeight As Double
        Get
            Return WordBoundingRect.Height
        End Get
    End Property
    Public Event PropertyChanged As PropertyChangedEventHandler
    Private Event INotifyPropertyChanged_PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged
    Public Sub New(OW As OcrWord)
        Word = OW
        UpdateProps(Word.BoundingRect)
    End Sub
    Public Sub Transform(scale As ScaleTransform)
        UpdateProps(scale.TransformBounds(Word.BoundingRect))
    End Sub
    Public Function CreateBorder(style As Style, Optional child As UIElement = Nothing) As Border
        Dim overlay = New Border() With {
                .Child = child,
                .Style = style
            }
        overlay.SetBinding(FrameworkElement.MarginProperty, CreateBinding("WordPosition"))
        overlay.SetBinding(FrameworkElement.WidthProperty, CreateBinding("WordWidth"))
        overlay.SetBinding(FrameworkElement.HeightProperty, CreateBinding("WordHeight"))
        Return overlay
    End Function
    Private Function CreateBinding(propertyName As String) As Binding
        Return New Binding() With {
                .Path = New PropertyPath(propertyName),
                .Source = Me
            }
    End Function
    Private Sub UpdateProps(wordBoundingBox As Rect)
        WordBoundingRect = wordBoundingBox
        OnPropertyChanged("WordPosition")
        OnPropertyChanged("WordWidth")
        OnPropertyChanged("WordHeight")
    End Sub
    Protected Sub OnPropertyChanged(PropertyName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(PropertyName))
    End Sub
End Class