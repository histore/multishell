using System;
using Avalonia.Controls;

namespace MultiShell.Views.Controls;

public partial class SettingsMenuButton : UserControl
{
    public event Action? ProfilesRequested;
    public event Action? HelpRequested;
    public event Action? AboutRequested;

    public SettingsMenuButton()
    {
        InitializeComponent();

        if (SettingsProfilesMenuItem != null)
        {
            SettingsProfilesMenuItem.Click += (_, _) =>
            {
                SettingsToolbarBtn?.Flyout?.Hide();
                ProfilesRequested?.Invoke();
            };
        }

        if (SettingsHelpMenuItem != null)
        {
            SettingsHelpMenuItem.Click += (_, _) =>
            {
                SettingsToolbarBtn?.Flyout?.Hide();
                HelpRequested?.Invoke();
            };
        }

        if (SettingsAboutMenuItem != null)
        {
            SettingsAboutMenuItem.Click += (_, _) =>
            {
                SettingsToolbarBtn?.Flyout?.Hide();
                AboutRequested?.Invoke();
            };
        }
    }
}
