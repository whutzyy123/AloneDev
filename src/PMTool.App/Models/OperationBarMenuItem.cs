using System.Windows.Input;

namespace PMTool.App.Models;

public sealed class OperationBarMenuItem
{
    public required string Text { get; init; }
    public required ICommand Command { get; init; }
}
