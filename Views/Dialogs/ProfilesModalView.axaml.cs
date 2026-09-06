using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace MultiShell.Views.Dialogs;

public partial class ProfilesModalView : UserControl
{
    public event EventHandler? CloseRequested;
    public event EventHandler? BrowseExecutableRequested;
    public event EventHandler? BrowseWorkingDirRequested;

    public ProfilesModalView()
    {
        InitializeComponent();

        if (BrowseExecutableBtn != null)
        {
            BrowseExecutableBtn.Click += (_, _) => BrowseExecutableRequested?.Invoke(this, EventArgs.Empty);
        }

        if (BrowseWorkingDirBtn != null)
        {
            BrowseWorkingDirBtn.Click += (_, _) => BrowseWorkingDirRequested?.Invoke(this, EventArgs.Empty);
        }

        if (ProfilesModalOverlay != null)
        {
            ProfilesModalOverlay.PointerPressed += (_, e) =>
            {
                if (e.Source == ProfilesModalOverlay)
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
            };
        }
    }
}
