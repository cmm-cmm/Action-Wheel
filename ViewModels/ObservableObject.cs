using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Action_Wheel.ViewModels
{
    /// <summary>
    /// The usual INotifyPropertyChanged base. Hand-written rather than taken from
    /// CommunityToolkit.Mvvm: this app needs two types from that package and nothing else, and the
    /// source generators it brings would be one more thing standing between a checkout and a build.
    /// </summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Raise([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>Raises for several properties at once, for the derived ones a change affects.</summary>
        protected void Raise(params string[] names)
        {
            foreach (var name in names)
                Raise(name);
        }

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            Raise(name);
            return true;
        }
    }
}
