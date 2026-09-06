using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace MultiShell.Views.Dialogs;

public partial class HelpModalView : UserControl
{
    public event EventHandler? CloseRequested;

    public HelpModalView()
    {
        InitializeComponent();

        if (CloseHelpModalButton != null)
        {
            CloseHelpModalButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        if (OkHelpModalButton != null)
        {
            OkHelpModalButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        if (HelpModalOverlay != null)
        {
            HelpModalOverlay.PointerPressed += (_, e) =>
            {
                if (e.Source == HelpModalOverlay)
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
            };
        }
    }
}
