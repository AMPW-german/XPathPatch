
using System;
using XPP.Doc;
using XPP.Utils;

namespace XPP.Path;

public struct ExecValue(XPValueType type)
{
  public XPValueType Type = type;
  public bool Bool;
  public double Number;
  public string String;
  public XPNodeRef Node;

  public ExecValue(bool val) : this(XPValueType.Bool) => Bool = val;
  public ExecValue(double val) : this(XPValueType.Number) => Number = val;
  public ExecValue(string val) : this(XPValueType.String) => String = val;
  public ExecValue(ReadOnlySpan<char> val) : this(XPValueType.String) => String = new(val);
  public ExecValue(XPNodeRef val) : this(XPValueType.NodeSet) => Node = val;

  public bool BoolValue => Type switch
  {
    XPValueType.Bool => Bool,
    XPValueType.Number => Number != 0 && !double.IsNaN(Number),
    XPValueType.String => String.Length > 0,
    XPValueType.NodeSet => Node.Valid,
    _ => throw new InvalidOperationException($"{Type}"),
  };

  private class DataBuf() : ThreadBuf<DataBuf, char>(Exec.MAX_DATA_SIZE);
  public string StringValue => Type switch
  {
    XPValueType.Bool => Exec.BoolStringValue(Bool),
    XPValueType.Number => Exec.NumberStringValue(Number),
    XPValueType.String => String,
    XPValueType.NodeSet when DataBuf.Span is Span<char> buf =>
      new(buf[..Node.Nav.StringValue(buf)]),
    _ => throw new InvalidOperationException($"{Type}"),
  };
}

public readonly struct XPathExecution(XPath Path, XPNodeRef Node)
{
  public readonly XPath Path = Path;
  public readonly XPNodeRef Node = Node;

  public Exec GetEnumerator() => new(Path, Node.Nav);
}

public ref partial struct Exec
{
  public bool MoveNext()
  {
    if (!started)
      result = GetValue(0, rootContext);
    if (started && result.Type != XPValueType.NodeSet)
      return false;
    started = true;
    if (result.Type == XPValueType.NodeSet)
    {
      if (!NextNode(result.NodeSet, out var nav))
        return false;
      resultNode = nav.Node;
    }
    return true;
  }

  public ExecValue Current => result.Type switch
  {
    XPValueType.Bool => new(result.Bool),
    XPValueType.Number => new(result.Number),
    XPValueType.String => new(result.String),
    XPValueType.NodeSet => new(resultNode),
    _ => throw new InvalidOperationException($"{result.Type}"),
  };
}