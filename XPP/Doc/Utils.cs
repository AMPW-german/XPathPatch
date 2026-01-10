
using System;
using System.Collections;
using System.Collections.Generic;

namespace XPP.Doc;

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
      
      return ref ChunkFor(index)[index & CHUNK_MASK];
    }
  }

  public ref T this[Index index] => ref this[index.GetOffset(length)];

  public int Add(T val)
  {
    var idx = length++;
    ChunkFor(idx)[idx & CHUNK_MASK] = val;
    return idx;
  }

  public int Add(ref readonly T val)
  {
    var idx = length++;
    ChunkFor(idx)[idx & CHUNK_MASK] = val;
    return idx;
  }

  private T[] ChunkFor(int index)
  {
    var cidx = index >> CHUNK_SHIFT;
    if (cidx == chunks.Count)
      chunks.Add(new T[CHUNK_SIZE]);
    return chunks[cidx];
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