using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace MultiShell.Views.Dialogs;

public partial class AboutModalView : UserControl
{
    public event EventHandler? CloseRequested;

    public AboutModalView()
    {
        InitializeComponent();

        if (OkAboutModalButton != null)
        {
            OkAboutModalButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        if (AboutModalOverlay != null)
        {
            AboutModalOverlay.PointerPressed += (_, e) =>
            {
                if (e.Source == AboutModalOverlay)
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
            };
        }
    }
}
