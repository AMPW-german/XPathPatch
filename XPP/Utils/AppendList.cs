
using System;
using System.Collections;
using System.Collections.Generic;

namespace XPP.Utils;

public class AppendList<T> : IEnumerable<T>, IDisposable where T : notnull
{
  protected const int CHUNK_SHIFT = 10;
  protected const int CHUNK_SIZE = 1 << CHUNK_SHIFT;
  protected const int CHUNK_MASK = CHUNK_SIZE - 1;

  protected readonly List<T[]> chunks = [];
  protected int length = 0;

  public int Length
  {
    get => length;
    set
    {
      if (value < 0 || value > length)
        throw new IndexOutOfRangeException($"{value}");
      length = value;
    }
  }

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

  protected ref T Ref(int index)
  {
    var (chunk, offset) = IndexToChunkOffset(index);
    if (chunk == chunks.Count)
      chunks.Add(NewChunk());
    return ref chunks[chunk][offset];
  }

  protected virtual T[] NewChunk() => new T[CHUNK_SIZE];

  protected static (int, int) IndexToChunkOffset(int index) =>
    (index >> CHUNK_SHIFT, index & CHUNK_MASK);

  public Enumerator GetEnumerator() => new(this);
  IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public virtual void Dispose() { }

  public struct Enumerator(AppendList<T> list) : IEnumerator<T>, IEnumerator
  {
    private readonly AppendList<T> list = list;
    private int index = -1;

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

public class PooledAppendList<T>() : AppendList<T> where T : notnull
{
  [ThreadStatic]
  private static Queue<WeakReference<T[]>> Pool;

  private readonly Queue<WeakReference<T[]>> pool = Pool ??= new();

  public override void Dispose()
  {
    foreach (var chunk in chunks)
    {
      Array.Fill(chunk, default);
      pool.Enqueue(new(chunk));
    }
    chunks.Clear();
    length = 0;
  }

  protected override T[] NewChunk()
  {
    while (pool.Count > 0)
    {
      var cref = pool.Dequeue();
      if (cref.TryGetTarget(out var chunk))
        return chunk;
    }
    return new T[CHUNK_SIZE];
  }
}