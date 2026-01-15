
using System;

namespace XPP.Utils;

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

  public static BufBorrow Borrow(ref SpanBuf<T> buf, int len)
  {
    var prev = buf.length;
    buf.length += len;
    return new(buf[prev..buf.length], ref buf.length, prev);
  }

  public ref struct BufBorrow(Span<T> Span, ref int bufLen, int prevLen) : IDisposable
  {
    public readonly Span<T> Span = Span;
    private readonly ref int bufLen = ref bufLen;
    private readonly int prevLen = prevLen;

    public void Dispose() => bufLen = prevLen;
  }
}

public class ThreadBuf<O, V>(int size) where O : ThreadBuf<O, V>, new()
{
  private readonly int size = size;
  private static readonly O Instance = new();

  [ThreadStatic]
  private static V[] buffer;

  public static SpanBuf<V> Buf => new(Span);

  public static Span<V> Span
  {
    get
    {
      var size = Instance.size;
      Span<V> buf = buffer ??= new V[size];
      return buf[..size];
    }
  }
}

public static partial class Extensions
{
  public static SpanBuf<T>.BufBorrow Borrow<T>(ref this SpanBuf<T> buf, int len) =>
    SpanBuf<T>.Borrow(ref buf, len);
}