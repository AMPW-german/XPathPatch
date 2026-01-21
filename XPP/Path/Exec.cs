
using System;
using System.Globalization;
using System.Text;
using XPP.Doc;

namespace XPP.Path;

public struct ExecResult(XPValueType type)
{
  public XPValueType Type = type;
  public bool Bool;
  public double Number;
  public string String;
  public ExecPathOp NodeSet;

  public ExecResult(bool val) : this(XPValueType.Bool) => Bool = val;
  public ExecResult(double val) : this(XPValueType.Number) => Number = val;
  public ExecResult(string val) : this(XPValueType.String) => String = val;
  public ExecResult(ReadOnlySpan<char> val) : this(XPValueType.String) => String = new(val);
  public ExecResult(ExecPathOp val) : this(XPValueType.NodeSet) => NodeSet = val;

  private ExecValue First => Type switch
  {
    XPValueType.Bool => new(Bool),
    XPValueType.Number => new(Number),
    XPValueType.String => new(String),
    XPValueType.NodeSet =>
      NodeSet.Next(out var next) ? new(next.Nav.Node) : new(XPNodeRef.Invalid),
    _ => throw new InvalidOperationException($"{Type}"),
  };

  public bool BoolValue => First.BoolValue;
  public double NumberValue => First.NumberValue;
  public string StringValue => First.StringValue;

  public Enumerator GetEnumerator() => new(this);

  public struct Enumerator(ExecResult res)
  {
    private readonly ExecResult res = res;
    private bool first = true;
    private ExecValue cur;

    public bool MoveNext()
    {
      if (res.Type != XPValueType.NodeSet && !first)
        return false;
      first = false;
      switch (res.Type)
      {
        case XPValueType.Bool:
          cur = new(res.Bool);
          return true;
        case XPValueType.Number:
          cur = new(res.Number);
          return true;
        case XPValueType.String:
          cur = new(res.String);
          return true;
        case XPValueType.NodeSet:
          if (!res.NodeSet.Next(out var next))
            return false;
          cur = new(next.Nav.Node);
          return true;
        default:
          throw new InvalidOperationException($"{res.Type}");
      }
    }

    public ExecValue Current => cur;
  }
}

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

  public double NumberValue => Type switch
  {
    XPValueType.Bool => Bool ? 1 : 0,
    XPValueType.Number => Number,
    XPValueType.String => StringNumberValue(String),
    XPValueType.NodeSet => NodeNumberValue(Node),
    _ => throw new InvalidOperationException($"{Type}"),
  };

  public string StringValue => Type switch
  {
    XPValueType.Bool => Bool ? "true" : "false",
    XPValueType.Number => NumberStringValue(Number),
    XPValueType.String => String,
    XPValueType.NodeSet => NodeStringValue(Node),
    _ => throw new InvalidOperationException($"{Type}"),
  };

  public static double StringNumberValue(string val) =>
    double.TryParse(
      val,
      NumberStyles.Integer | NumberStyles.AllowDecimalPoint,
      null,
      out double parsed
    ) ? parsed : double.NaN;

  public static double NodeNumberValue(XPNodeRef node) =>
    StringNumberValue(NodeStringValue(node));

  public static string NumberStringValue(double num)
  {
    if (double.IsNaN(num))
      return "NaN";
    if (num == 0 || num == -0)
      return "0";
    if (double.IsPositiveInfinity(num))
      return "Infinity";
    if (double.IsNegativeInfinity(num))
      return "-Infinity";

    return $"{num:0.#################}";
  }

  public static string NodeStringValue(XPNodeRef node)
  {
    var sb = new StringBuilder();
    node.Nav.StringValue(sb);
    return sb.ToString();
  }
}