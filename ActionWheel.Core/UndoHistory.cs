using System;
using System.Collections.Generic;
using System.Linq;

namespace Action_Wheel.Core
{
    /// <summary>Snapshot-based undo/redo history with structural sequence equality.</summary>
    public sealed class UndoHistory<T>
    {
        private readonly Stack<IReadOnlyList<T>> _undo = new();
        private readonly Stack<IReadOnlyList<T>> _redo = new();
        private IReadOnlyList<T> _current;

        public UndoHistory(IEnumerable<T> initial) => _current = initial.ToArray();

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public bool Capture(IEnumerable<T> values)
        {
            var next = values.ToArray();
            if (_current.SequenceEqual(next))
                return false;

            _undo.Push(_current);
            _redo.Clear();
            _current = next;
            return true;
        }

        /// <summary>
        /// Rewrites every snapshot (current, undo and redo) with <paramref name="map"/>, keeping
        /// their order. For a change that has to reach history too, such as a profile rename:
        /// without it, Undo brought the old name back onto the buttons.
        /// </summary>
        public void Map(Func<IReadOnlyList<T>, IReadOnlyList<T>> map)
        {
            _current = map(_current);
            Rebuild(_undo);
            Rebuild(_redo);

            void Rebuild(Stack<IReadOnlyList<T>> stack)
            {
                // A stack enumerates top first; reversing gives the order to push back in.
                var mapped = stack.Reverse().Select(map).ToArray();
                stack.Clear();
                foreach (var snapshot in mapped)
                    stack.Push(snapshot);
            }
        }

        public bool TryUndo(out IReadOnlyList<T> values)
        {
            if (_undo.Count == 0)
            {
                values = _current;
                return false;
            }

            _redo.Push(_current);
            _current = _undo.Pop();
            values = _current;
            return true;
        }

        public bool TryRedo(out IReadOnlyList<T> values)
        {
            if (_redo.Count == 0)
            {
                values = _current;
                return false;
            }

            _undo.Push(_current);
            _current = _redo.Pop();
            values = _current;
            return true;
        }
    }
}
