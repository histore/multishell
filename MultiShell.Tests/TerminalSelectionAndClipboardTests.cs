using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using MultiShell.Services;
using MultiShell.ViewModels;
using MultiShell.Views;
using SvcSystems.UI.Terminal;
using Xunit;

namespace MultiShell.Tests;

/// <summary>
/// Unit tests for terminal text selection, model synchronization, and clipboard interactions.
/// </summary>
[Collection("HeadlessUI")]
public class TerminalSelectionAndClipboardTests
{
    [Fact]
    public async Task HandleSelection_SyncsBothControlAndModel()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var ctrl = new TerminalControl();
            var model = new TerminalControlModel();
            ctrl.Model = model;
            model.Resize(800, 600, 8, 16);
            model.Feed("Hello World\r\nSecond Line\r\n");

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var mPressed = ctrl.GetType().GetMethod("HandleSelectionPressed", flags);
            var mMoved = ctrl.GetType().GetMethod("HandleSelectionMoved", flags);
            var mReleased = ctrl.GetType().GetMethod("HandleSelectionReleased", flags);

            Assert.NotNull(mPressed);
            Assert.NotNull(mMoved);
            Assert.NotNull(mReleased);

            var delPressed = (Func<Point, KeyModifiers, int, bool>)Delegate.CreateDelegate(typeof(Func<Point, KeyModifiers, int, bool>), ctrl, mPressed);
            var delMoved = (Func<Point, bool>)Delegate.CreateDelegate(typeof(Func<Point, bool>), ctrl, mMoved);
            var delReleased = (Func<Point, KeyModifiers, int, bool>)Delegate.CreateDelegate(typeof(Func<Point, KeyModifiers, int, bool>), ctrl, mReleased);

            delPressed(new Point(0, 0), KeyModifiers.None, 1);
            delMoved(new Point(40, 0));
            delReleased(new Point(40, 0), KeyModifiers.None, 1);

            Assert.True(ctrl.HasSelection);
            Assert.True(model.HasSelection);
            Assert.Equal("Hello", ctrl.SelectedText);
            Assert.Equal("Hello", model.SelectedText);

            model.ClearSelection();
            Assert.False(ctrl.HasSelection);
            Assert.False(model.HasSelection);
        });
    }

    [Fact]
    public void SoftSelectionStart_And_DragExtend_PopulatesModelSelection()
    {
        var model = new TerminalControlModel();
        model.Resize(800, 600, 8, 16);
        model.Feed("MultiShell terminal selection test\r\n");

        model.SetSoftSelectionStart(0, 0);
        model.StartSelectionFromSoftStart();
        model.DragExtendSelection(0, 10);

        Assert.True(model.HasSelection);
        Assert.Equal("MultiShell", model.SelectedText);
    }

    [Fact]
    public async Task ModifierKeys_InTerminalPaneView_DoNotDestroyActiveSelection()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var view = new TerminalPaneView();
            var model = new TerminalControlModel();
            model.Resize(800, 600, 8, 16);
            model.Feed("Selection text\r\n");
            model.SelectWordOrExpression(0, 0);

            Assert.True(model.HasSelection);

            // Simulating pressing LeftCtrl, LeftShift
            var onKeyDownMethod = typeof(TerminalPaneView).GetMethod("OnTerminalKeyDown", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(onKeyDownMethod);

            // LeftCtrl KeyDown
            var ctrlArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.LeftCtrl,
                KeyModifiers = KeyModifiers.Control
            };
            onKeyDownMethod.Invoke(view, new object[] { view, ctrlArgs });
            Assert.True(ctrlArgs.Handled, "LeftCtrl should be marked handled to avoid clearing selection");
            Assert.True(model.HasSelection, "LeftCtrl must not clear selection");

            // LeftShift KeyDown
            var shiftArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.LeftShift,
                KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift
            };
            onKeyDownMethod.Invoke(view, new object[] { view, shiftArgs });
            Assert.True(shiftArgs.Handled, "LeftShift should be marked handled to avoid clearing selection");
            Assert.True(model.HasSelection, "LeftShift must not clear selection");
        });
    }

    private sealed class TestSession : IShellSession
    {
        public Guid SessionId => Guid.NewGuid();
        public string Title => "Test";
        public string? WorkingDirectory { get; set; }
        public Models.ShellType ShellType => Models.ShellType.PowerShell;
        public bool IsRunning { get; set; } = true;
        public System.Collections.Generic.List<byte[]> SentPayloads { get; } = new();
        public event Action<ReadOnlyMemory<byte>>? DataReceived { add { } remove { } }
        public event Action<int>? Exited { add { } remove { } }
        public event Action<string>? WorkingDirectoryChanged { add { } remove { } }
        public event Action<string>? CommandExecuted { add { } remove { } }
        public event Action<int>? CommandCompleted { add { } remove { } }
        public void Start() { }
        public void Send(byte[] data) => SentPayloads.Add(data);
        public void Resize(int cols, int rows) { }
        public void Dispose() { }
    }

    [Fact]
    public void PasteText_WhenBracketedPasteModeActive_WrapsWithEsc200And201()
    {
        var session = new TestSession();
        var tabVm = new TerminalTabViewModel(session);
        var paneVm = tabVm.ActivePane!;

        // Enable bracketed paste mode (e.g. Copilot CLI \e[?2004h)
        paneVm.TerminalModel.Terminal.Feed("\x1b[?2004h");
        Assert.True(paneVm.TerminalModel.Terminal.Engine.BracketedPasteMode);

        paneVm.PasteText("copilot prompt input");

        Assert.Single(session.SentPayloads);
        var sentString = System.Text.Encoding.UTF8.GetString(session.SentPayloads[0]);
        Assert.Equal("\x1b[200~copilot prompt input\x1b[201~", sentString);
    }

    [Fact]
    public void PasteText_WhenBracketedPasteModeInactive_SendsRawPayload()
    {
        var session = new TestSession();
        var tabVm = new TerminalTabViewModel(session);
        var paneVm = tabVm.ActivePane!;

        Assert.False(paneVm.TerminalModel.Terminal.Engine.BracketedPasteMode);

        paneVm.PasteText("standard powershell paste");

        Assert.Single(session.SentPayloads);
        var sentString = System.Text.Encoding.UTF8.GetString(session.SentPayloads[0]);
        Assert.Equal("standard powershell paste", sentString);
    }

    [Fact]
    public async Task SuppressMouseTrackingMode_ClearsIsMouseModeActive_AndEnablesNativeSelection()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var session = new TestSession();
            var tabVm = new TerminalTabViewModel(session);
            var paneVm = tabVm.ActivePane!;
            paneVm.TerminalModel.Terminal.Resize(80, 25);
            paneVm.TerminalModel.Terminal.Feed("Copilot CLI output prompt text\r\n");

            // Copilot CLI enables mouse tracking mode \e[?1000h
            paneVm.TerminalModel.Terminal.Feed("\x1b[?1000h");
            Assert.True(paneVm.TerminalModel.IsMouseModeActive);

            var ctrl = new TerminalControl { Model = paneVm.TerminalModel };
            ctrl.Width = 800;
            ctrl.Height = 600;
            ctrl.Measure(new Size(800, 600));
            ctrl.Arrange(new Rect(0, 0, 800, 600));

            // Suppress mouse tracking mode
            paneVm.SuppressMouseTrackingMode();
            Assert.False(paneVm.TerminalModel.IsMouseModeActive);

            // Native selection now succeeds on pointer drag
            var pressArgs = new PointerPressedEventArgs(
                ctrl,
                new Avalonia.Input.Pointer(1, PointerType.Mouse, true),
                ctrl,
                new Point(10, 10),
                0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None);
            ctrl.RaiseEvent(pressArgs);

            var moveArgs = new PointerEventArgs(
                InputElement.PointerMovedEvent,
                ctrl,
                new Avalonia.Input.Pointer(1, PointerType.Mouse, true),
                ctrl,
                new Point(200, 10),
                10,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
                KeyModifiers.None);
            ctrl.RaiseEvent(moveArgs);

            var releaseArgs = new PointerReleasedEventArgs(
                ctrl,
                new Avalonia.Input.Pointer(1, PointerType.Mouse, true),
                ctrl,
                new Point(200, 10),
                20,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
                KeyModifiers.None,
                MouseButton.Left);
            ctrl.RaiseEvent(releaseArgs);

            Assert.True(ctrl.HasSelection);
            Assert.True(paneVm.TerminalModel.HasSelection);
            Assert.False(string.IsNullOrEmpty(ctrl.SelectedText));
        });
    }
}
