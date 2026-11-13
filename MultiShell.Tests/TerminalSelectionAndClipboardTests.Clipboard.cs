using System.Threading.Tasks;
using MultiShell.ViewModels;
using MultiShell.Views;
using Xunit;

namespace MultiShell.Tests;

public partial class TerminalSelectionAndClipboardTests
{
    [Fact]
    public async Task TryCopySelectionAsync_WhenClipboardThrowsCOMException_HandlesGracefullyAndClearsSelection()
    {
        await HeadlessTestSession.DispatchAsync(async () =>
        {
            var session = new TestSession();
            var tabVm = new TerminalTabViewModel(session);
            var paneVm = tabVm.ActivePane!;

            var nativeModel = (SvcSystems.UI.Terminal.TerminalControlModel)paneVm.TerminalModel.NativeModel;
            nativeModel.Resize(800, 600, 8, 16);
            paneVm.TerminalModel.Feed("Selected text to copy\r\n");

            nativeModel.SelectAll();
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
