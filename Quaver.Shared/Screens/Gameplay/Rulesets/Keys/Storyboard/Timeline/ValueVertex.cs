using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;

namespace Quaver.Shared.Screens.Gameplay.Rulesets.Keys.Storyboard.Timeline;

[MoonSharpUserData]
public class ValueVertex<T>
{
    public int Time { get; init; }
    public T Payload { get; init; }
    public int Id { get; init; }
    public bool IsDynamic { get; init; }

    private sealed class TimeSegmentIdRelationalComparer : IComparer<ValueVertex<T>>
    {
        public int Compare(ValueVertex<T> x, ValueVertex<T> y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (ReferenceEquals(null, y)) return 1;
            if (ReferenceEquals(null, x)) return -1;
            var timeComparison = x.Time.CompareTo(y.Time);
            if (timeComparison != 0) return timeComparison;
            return x.Id.CompareTo(y.Id);
        }
    }

    public static IComparer<ValueVertex<T>> TimeSegmentIdComparer { get; } = new TimeSegmentIdRelationalComparer();

    public bool Equals(ValueVertex<T> x, ValueVertex<T> y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.Time == y.Time && EqualityComparer<T>.Default.Equals(x.Payload, y.Payload) && Equals(x.Id, y.Id);
    }

    public int GetHashCode(ValueVertex<T> obj)
    {
        return HashCode.Combine(obj.Time, obj.Payload, obj.Id);
    }
}