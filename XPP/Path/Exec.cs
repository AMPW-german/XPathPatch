
using System;
using XPP.Doc;
using XPP.Utils;

namespace XPP.Path;

public ref partial struct Exec(XPath path, XPNavigator ctx) : IDisposable
{
  private const int MAX_SORT_NODESET = 0x10000;
  public const int MAX_DATA_SIZE = 0x10000;

  private class DataBuf() : ThreadBuf<DataBuf, char>(MAX_DATA_SIZE);
  private class StringBuf() : ThreadBuf<StringBuf, string>(MAX_SORT_NODESET);
  private class ArgBuf() : ThreadBuf<ArgBuf, Value>(MAX_SORT_NODESET);

  private const string TRUE = "true";
  private const string FALSE = "false";
  private const string NAN = "NaN";
  private const string ZERO = "0";
  private const string PINF = "Infinity";
  private const string NINF = "-Infinity";

  private readonly XPath path = path;
  private readonly XPNavigator rootContext = ctx;

  private readonly PathBuffers[] pathBufs = path.Paths.Length > 0
    ? new PathBuffers[path.Paths[^1].RootNum + 1]
    : [];
  private readonly Span<char> dataBuf = DataBuf.Span;
  private SpanBuf<string> stringBuf = StringBuf.Buf;
  private SpanBuf<Value> argBuf = ArgBuf.Buf;

  private bool started = false;
  private Value result;
  private int resultIndex;
  private XPNodeRef resultNode;

  private PooledAppendList<PathContext> PathResult(int entry) =>
    pathBufs[path.Paths[entry].RootNum].Nodes;
}