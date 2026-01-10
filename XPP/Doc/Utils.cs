
using System;
using System.Collections;
using System.Collections.Generic;

namespace XPP.Doc;

public class AppendList<T> : IEnumerable<T> where T : notnull
{
  private const int INITIAL_SIZE = 10;
  private T[] values = new T[INITIAL_SIZE];
  private int length = 0;

  public int Length => length;

  public ref T this[int index]
  {
    get
    {
      if (index < 0 || index >= length)
        throw new IndexOutOfRangeException($"{index} <> [0,{length})");
      return ref values[index];
    }
  }

  public ref T this[Index index] => ref this[index.GetOffset(length)];

  public Span<T> this[Range range]
  {
    get
    {
      var (start, len) = range.GetOffsetAndLength(length);
      return values.AsSpan(start, len);
    }
  }

  public int Add(T val)
  {
    EnsureCap(length + 1);
    var idx = length++;
    values[idx] = val;
    return idx;
  }

  public int Add(ref readonly T val)
  {
    EnsureCap(length + 1);
    var idx = length++;
    values[idx] = val;
    return idx;
  }

  public Range AddRange(ReadOnlySpan<T> vals)
  {
    EnsureCap(length + vals.Length);
    var start = length;
    vals.CopyTo(values.AsSpan(start));
    length += vals.Length;
    return start..length;
  }

  private void EnsureCap(int cap)
  {
    if (values.Length >= cap)
      return;
    var newCap = Math.Max(cap, values.Length * 2);
    var newVals = new T[newCap];
    values.CopyTo(newVals);
    values = newVals;
  }

  public Enumerator GetEnumerator() => new(this);
  IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public struct Enumerator(AppendList<T> list) : IEnumerator<T>, IEnumerator
  {
    private readonly AppendList<T> list = list;
    private int index;

    public T Current => list[index];

    object IEnumerator.Current => Current;

    public void Dispose() { }
    public bool MoveNext() => ++index < list.length;
    public void Reset() => index = -1;
  }
}