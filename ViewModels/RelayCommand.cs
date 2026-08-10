using System;
using System.Windows.Input;

namespace Action_Wheel.ViewModels
{
    /// <summary>An <see cref="ICommand"/> over a delegate, so a Button can bind to a method.</summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        /// <summary>
        /// Re-asks <c>canExecute</c>. Must be called by hand when the state it reads changes -
        /// there is no CommandManager in WinUI 3 to notice on the command's behalf.
        /// </summary>
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
