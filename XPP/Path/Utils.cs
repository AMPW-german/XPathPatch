
using System;

namespace XPP.Path;

public ref struct SpanBuf<T>(Span<T> buf)
{
  private readonly Span<T> buf = buf;
  private int length;

  public int Length
  {
    get => length;
    set
    {
      if (value < 0 || value > length)
        throw new IndexOutOfRangeException();
      length = value;
    }
  }
  public int Cap => buf.Length;

  public ref T this[int index] => ref buf[index];

  public Span<T> this[Range range] => buf[range];

  public Span<T> Span => buf[..length];

  public int Add(T val)
  {
    buf[length] = val;
    return length++;
  }

  public Range AddRange(ReadOnlySpan<T> vals)
  {
    var start = length;
    vals.CopyTo(buf[length..]);
    length += vals.Length;
    return start..length;
  }

  public Span<T> Rest => buf[length..];
  public Range AddRest(int len)
  {
    var start = length;
    length += len;
    return start..length;
  }
}

public interface IThreadBuf
{
  public static abstract int Size { get; }
}

public class ThreadBuf<O, V> where O : ThreadBuf<O, V>, IThreadBuf
{
  [ThreadStatic]
  private static V[] buffer;

  public static SpanBuf<V> Buf => new(Span);

  public static Span<V> Span
  {
    get
    {
      var size = O.Size;
      Span<V> buf = buffer ??= new V[size];
      return buf[..size];
    }
  }
}