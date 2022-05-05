﻿NotInheritable Class App
    Inherits Application
    Protected Overrides Sub OnLaunched(e As LaunchActivatedEventArgs)
        Dim rootFrame As Frame = TryCast(Window.Current.Content, Frame)
        If rootFrame Is Nothing Then
            rootFrame = New Frame()
            AddHandler rootFrame.NavigationFailed, AddressOf OnNavigationFailed
            If e.PreviousExecutionState = ApplicationExecutionState.Terminated Then
            End If
            Window.Current.Content = rootFrame
        End If
        If e.PrelaunchActivated = False Then
            If rootFrame.Content Is Nothing Then
                rootFrame.Navigate(GetType(CrystalFilePage), e.Arguments)
            End If
            Dim uwpTitleBar As ApplicationViewTitleBar = ApplicationView.GetForCurrentView().TitleBar
            uwpTitleBar.ButtonBackgroundColor = Windows.UI.Colors.Transparent
            uwpTitleBar.BackgroundColor = Windows.UI.Colors.Transparent
            Dim coreTitleBar As Core.CoreApplicationViewTitleBar = Core.CoreApplication.GetCurrentView().TitleBar
            coreTitleBar.ExtendViewIntoTitleBar = True
            Window.Current.Activate()
        End If
    End Sub
    Private Sub OnNavigationFailed(sender As Object, e As NavigationFailedEventArgs)
        Throw New Exception("Failed to load Page " + e.SourcePageType.FullName)
    End Sub
    Private Sub OnSuspending(sender As Object, e As SuspendingEventArgs) Handles Me.Suspending
        Dim deferral As SuspendingDeferral = e.SuspendingOperation.GetDeferral()
        deferral.Complete()
    End Sub
End Class