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

    [Fact]
    public async Task PageUp_And_PageDown_InNormalBuffer_ScrollBuffer()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var session = new TestSession();
            var tabVm = new TerminalTabViewModel(session);
            var paneVm = tabVm.ActivePane!;
            var view = new TerminalPaneView { DataContext = paneVm };

            // Fill with scrollback lines
            paneVm.TerminalModel.Resize(800, 300, 8, 16);
            for (int i = 0; i < 50; i++)
            {
                paneVm.TerminalModel.Feed($"Line {i}\r\n");
            }

            var initialYDisp = paneVm.TerminalModel.Terminal.Buffer.YDisp;
            Assert.True(initialYDisp > 0, "Buffer should have scrolled down initially");

            var onKeyDownMethod = typeof(TerminalPaneView).GetMethod("OnTerminalKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!;

            // PageUp KeyDown
            var upArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.PageUp,
                KeyModifiers = KeyModifiers.None
            };
            onKeyDownMethod.Invoke(view, new object[] { view, upArgs });

            Assert.True(upArgs.Handled);
            Assert.True(paneVm.TerminalModel.Terminal.Buffer.YDisp < initialYDisp, "PageUp should scroll buffer up");

            // PageDown KeyDown
            var downArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.PageDown,
                KeyModifiers = KeyModifiers.None
            };
            onKeyDownMethod.Invoke(view, new object[] { view, downArgs });

            Assert.True(downArgs.Handled);
            Assert.Equal(initialYDisp, paneVm.TerminalModel.Terminal.Buffer.YDisp);
        });
    }

    [Fact]
    public async Task PageUp_And_PageDown_InAltBuffer_SendVtSequences()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var session = new TestSession();
            var tabVm = new TerminalTabViewModel(session);
            var paneVm = tabVm.ActivePane!;
            var view = new TerminalPaneView { DataContext = paneVm };

            // Switch to Alternate Buffer (as used by htop, nano, vim, less)
            paneVm.TerminalModel.Terminal.SwitchToAltBuffer();
            Assert.True(paneVm.TerminalModel.Terminal.IsAlternateBufferActive);

            var onKeyDownMethod = typeof(TerminalPaneView).GetMethod("OnTerminalKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!;

            // PageUp in Alt Buffer -> Send ESC [ 5 ~
            var upArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.PageUp,
                KeyModifiers = KeyModifiers.None
            };
            onKeyDownMethod.Invoke(view, new object[] { view, upArgs });

            Assert.True(upArgs.Handled);
            Assert.Single(session.SentPayloads);
            Assert.Equal("\u001b[5~", System.Text.Encoding.UTF8.GetString(session.SentPayloads[0]));

            // PageDown in Alt Buffer -> Send ESC [ 6 ~
            var downArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.PageDown,
                KeyModifiers = KeyModifiers.None
            };
            onKeyDownMethod.Invoke(view, new object[] { view, downArgs });

            Assert.True(downArgs.Handled);
            Assert.Equal(2, session.SentPayloads.Count);
            Assert.Equal("\u001b[6~", System.Text.Encoding.UTF8.GetString(session.SentPayloads[1]));
        });
    }

    [Fact]
    public async Task PageUp_WithAltModifier_SendsAltVtSequence()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var session = new TestSession();
            var tabVm = new TerminalTabViewModel(session);
            var paneVm = tabVm.ActivePane!;
            var view = new TerminalPaneView { DataContext = paneVm };

            var onKeyDownMethod = typeof(TerminalPaneView).GetMethod("OnTerminalKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var upAltArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.PageUp,
                KeyModifiers = KeyModifiers.Alt
            };
            onKeyDownMethod.Invoke(view, new object[] { view, upAltArgs });

            Assert.True(upAltArgs.Handled);
            Assert.Single(session.SentPayloads);
            Assert.Equal("\u001b[5;3~", System.Text.Encoding.UTF8.GetString(session.SentPayloads[0]));
        });
    }

    [Theory]
    [InlineData(Key.Enter, KeyModifiers.Shift)]
    [InlineData(Key.Enter, KeyModifiers.Control)]
    public async Task ShiftEnter_And_CtrlEnter_SendLineFeed(Key key, KeyModifiers modifiers)
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var session = new TestSession();
            var tabVm = new TerminalTabViewModel(session);
            var paneVm = tabVm.ActivePane!;
            var view = new TerminalPaneView { DataContext = paneVm };

            var onKeyDownMethod = typeof(TerminalPaneView).GetMethod("OnTerminalKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var enterArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = key,
                KeyModifiers = modifiers
            };
            onKeyDownMethod.Invoke(view, new object[] { view, enterArgs });

            Assert.True(enterArgs.Handled);
            Assert.Single(session.SentPayloads);
            Assert.Equal(new byte[] { 0x0A }, session.SentPayloads[0]);
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

    [Fact]
    public async Task TryGetTerminalWordOrLine_WithBufferContent_ResolvesLineAndColCoordinates()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var session = new TestSession();
            var tabVm = new TerminalTabViewModel(session);
            var paneVm = tabVm.ActivePane!;

            paneVm.TerminalModel.Resize(800, 600, 8, 16);
            paneVm.TerminalModel.Feed("Visit https://github.com/histore/multishell for code\r\n");

            var ctrl = new TerminalControl { Model = paneVm.TerminalModel };
            ctrl.FontSize = 14;

            // Coordinates on the first line (e.g. x = 80, y = 8)
            var success = TerminalPaneView.TryGetTerminalWordOrLine(
                ctrl,
                paneVm,
                new Point(80, 8),
                out var lineText,
                out var colIndex,
                out var row,
                out var charWidth,
                out var charHeight);

            Assert.True(success);
            Assert.Contains("https://github.com/histore/multishell", lineText);
            Assert.Equal(0, row);
            Assert.True(colIndex > 0);
            Assert.True(charWidth > 0);
            Assert.True(charHeight > 0);
        });
    }

    [Fact]
    public async Task TryGetTerminalWordOrLine_WithScrolledBuffer_RespectsYDispOffset()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var session = new TestSession();
            var tabVm = new TerminalTabViewModel(session);
            var paneVm = tabVm.ActivePane!;

            paneVm.TerminalModel.Terminal.Resize(80, 10);
            for (int i = 0; i < 30; i++)
            {
                paneVm.TerminalModel.Terminal.Feed($"Line {i:D2}: output text\r\n");
            }

            var ctrl = new TerminalControl { Model = paneVm.TerminalModel };
            ctrl.FontSize = 14;

            var buffer = paneVm.TerminalModel.Terminal.Buffer;
            // Scroll to top
            buffer.ScrollToTop();
            Assert.Equal(0, buffer.YDisp);

            var successTop = TerminalPaneView.TryGetTerminalWordOrLine(
                ctrl,
                paneVm,
                new Point(40, 8),
                out var lineTextTop,
                out _,
                out var rowTop,
                out _,
                out _);

            Assert.True(successTop);
            Assert.Equal(0, rowTop);
            Assert.StartsWith("Line 00:", lineTextTop);

            // Scroll down to bottom
            buffer.ScrollToBottom();
            var successBottom = TerminalPaneView.TryGetTerminalWordOrLine(
                ctrl,
                paneVm,
                new Point(40, 8),
                out var lineTextBottom,
                out _,
                out var rowBottom,
                out _,
                out _);

            Assert.True(successBottom);
            Assert.Equal(0, rowBottom);
            // After scrolling to bottom, row 0 in viewport points to scrolled content, not Line 00
            Assert.NotEqual(lineTextTop, lineTextBottom);
        });
    }

    [Fact]
    public async Task TryCopySelectionAsync_WhenClipboardThrowsCOMException_HandlesGracefullyAndClearsSelection()
    {
        await HeadlessTestSession.DispatchAsync(async () =>
        {
            var session = new TestSession();
            var tabVm = new TerminalTabViewModel(session);
            var paneVm = tabVm.ActivePane!;

            paneVm.TerminalModel.Resize(800, 600, 8, 16);
            paneVm.TerminalModel.Feed("Selected text to copy\r\n");

            // Select text in model
            paneVm.TerminalModel.SelectAll();
            Assert.True(paneVm.TerminalModel.HasSelection);

            var paneView = new TerminalPaneView
            {
                DataContext = paneVm,
                SetClipboardTextOverride = _ => throw new System.Runtime.InteropServices.COMException("CLIPBRD_E_CANT_OPEN: OpenClipboard Failed", unchecked((int)0x800401D0))
            };

            // Act - must not throw COMException
            var copied = await paneView.TryCopySelectionAsync(paneVm);

            // Assert
            Assert.True(copied);
            Assert.False(paneVm.TerminalModel.HasSelection, "Selection should be cleared even if clipboard locked.");
        });
    }

    [Fact]
    public async Task PasteFromClipboardAsync_WhenClipboardThrows_DoesNotCrash()
    {
        await HeadlessTestSession.DispatchAsync(async () =>
        {
            var session = new TestSession();
            var tabVm = new TerminalTabViewModel(session);
            var paneVm = tabVm.ActivePane!;

            var paneView = new TerminalPaneView
            {
                DataContext = paneVm,
                GetClipboardTextOverride = () => throw new System.Runtime.InteropServices.COMException("CLIPBRD_E_CANT_OPEN: OpenClipboard Failed", unchecked((int)0x800401D0))
            };

            // Act - must not throw
            await paneView.PasteFromClipboardAsync(paneVm);

            // Assert: No payload sent since clipboard failed
            Assert.Empty(session.SentPayloads);
        });
    }
}
