using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MultiShell.ViewModels;
using MultiShell.Views;

namespace MultiShell;

/// <summary>
/// AOT-safe data template that maps view models to their corresponding views without reflection.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        return param switch
        {
            TerminalTabViewModel => new TerminalTabView(),
            _ => new TextBlock { Text = "Not Found: " + param.GetType().Name }
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
