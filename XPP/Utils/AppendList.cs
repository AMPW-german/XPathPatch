
using System;
using System.Collections;
using System.Collections.Generic;

namespace XPP.Utils;

public class AppendList<T> : IEnumerable<T> where T : notnull
{
  private const int CHUNK_SHIFT = 10;
  private const int CHUNK_SIZE = 1 << CHUNK_SHIFT;
  private const int CHUNK_MASK = CHUNK_SIZE - 1;

  private readonly List<T[]> chunks = [];
  private int length = 0;

  public int Length => length;

  public ref T this[int index]
  {
    get
    {
      if (index < 0 || index >= length)
        throw new IndexOutOfRangeException($"{index} <> [0,{length})");
      return ref Ref(index);
    }
  }

  public ref T this[Index index] => ref this[index.GetOffset(length)];

  public RangeEnumerator this[Range range] => new(this, range);

  public int Add(T val)
  {
    var idx = length++;
    Ref(idx) = val;
    return idx;
  }

  public int Add(ref readonly T val)
  {
    var idx = length++;
    Ref(idx) = val;
    return idx;
  }

  public Range AddRange(params Span<T> vals)
  {
    // TODO: use span copy per chunk
    var start = length;
    for (var i = 0; i < vals.Length; i++)
      Ref(length++) = vals[i];
    return start..length;
  }

  private ref T Ref(int index)
  {
    var (chunk, offset) = IndexToChunkOffset(index);
    if (chunk == chunks.Count)
      chunks.Add(new T[CHUNK_SIZE]);
    return ref chunks[chunk][offset];
  }

  private static (int, int) IndexToChunkOffset(int index)
  {
    return (index >> CHUNK_SHIFT, index & CHUNK_MASK);
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

  public struct RangeEnumerator(AppendList<T> list, Range range)
  {
    private readonly AppendList<T> list = list;
    private readonly (int off, int len) range = range.GetOffsetAndLength(list.Length);
    private int index = -1;

    public RangeEnumerator GetEnumerator() => this;

    public bool MoveNext() => ++index < range.len;
    public ref T Current => ref list[range.off + index];

    public int Offset => range.off;
    public int Length => range.len;
    public ref T this[Index index] => ref list[range.off + index.GetOffset(range.len)];
  }
}