
using System;
using XPP.Doc;

namespace XPP.Path;

public class ExecExprOpFunc(LibraryFunc Func, ExecExprOp[] Args) : ExecExprOp()
{
  public readonly LibraryFunc Func = Func;
  public readonly ExecExprOp[] Args = Args;
  private readonly ExecResult[] argVals = new ExecResult[Args.Length];

  private delegate ExecResult FuncDelegate(ExecPathCtx ctx, ExecResult[] args);
  private FuncDelegate funcDelegate;

  public override ExecResult Value(ExecPathCtx context)
  {
    funcDelegate ??= Func switch
    {
      LibraryFunc.Last => LibraryFuncLast,
      LibraryFunc.Position => LibraryFuncPosition,
      LibraryFunc.Count => LibraryFuncCount,
      LibraryFunc.Id => LibraryFuncId,
      LibraryFunc.LocalName => LibraryFuncLocalName,
      LibraryFunc.NamespaceUri => LibraryFuncNamespaceUri,
      LibraryFunc.Name => LibraryFuncName,
      LibraryFunc.String => LibraryFuncString,
      LibraryFunc.Concat => LibraryFuncConcat,
      LibraryFunc.StartsWith => LibraryFuncStartsWith,
      LibraryFunc.Contains => LibraryFuncContains,
      LibraryFunc.SubstringBefore => LibraryFuncSubstringBefore,
      LibraryFunc.SubstringAfter => LibraryFuncSubstringAfter,
      LibraryFunc.Substring => LibraryFuncSubstring,
      LibraryFunc.StringLength => LibraryFuncStringLength,
      LibraryFunc.NormalizeSpace => LibraryFuncNormalizeSpace,
      LibraryFunc.Translate => LibraryFuncTranslate,
      LibraryFunc.Boolean => LibraryFuncBoolean,
      LibraryFunc.Not => LibraryFuncNot,
      LibraryFunc.True => LibraryFuncTrue,
      LibraryFunc.False => LibraryFuncFalse,
      LibraryFunc.Lang => LibraryFuncLang,
      LibraryFunc.Number => LibraryFuncNumber,
      LibraryFunc.Sum => LibraryFuncSum,
      LibraryFunc.Floor => LibraryFuncFloor,
      LibraryFunc.Ceiling => LibraryFuncCeiling,
      LibraryFunc.Round => LibraryFuncRound,
      _ => throw new InvalidOperationException($"{Func}"),
    };
    for (var i = 0; i < Args.Length; i++)
      argVals[i] = Args[i].Value(context);
    return funcDelegate(context, argVals);
  }

  private static void AssertArgType(
    string fname, int argi, XPValueType expected, ref readonly ExecResult val)
  {
    if (val.Type == expected)
      return;
    throw new InvalidOperationException(
      $"argument {argi} to {fname} must be {expected}, not {val.Type}");
  }

  private ExecResult LibraryFuncLast(ExecPathCtx ctx, ExecResult[] args) =>
    new(ctx.Set.Length);
  private ExecResult LibraryFuncPosition(ExecPathCtx ctx, ExecResult[] args) =>
    new(ctx.Position + 1);

  private ExecResult LibraryFuncCount(ExecPathCtx ctx, ExecResult[] args)
  {
    AssertArgType("count", 0, XPValueType.NodeSet, in args[0]);

    var count = 0;
    foreach (var node in args[0].NodeSet)
      count++;

    return new(count);
  }

  private ExecResult LibraryFuncId(ExecPathCtx ctx, ExecResult[] args) =>
    throw new NotImplementedException();

  private ExecResult LibraryFuncLocalName(ExecPathCtx ctx, ExecResult[] args)
  {
    var node = ctx.Nav.Node;
    if (args.Length > 0)
    {
      AssertArgType("local-name", 0, XPValueType.NodeSet, in args[0]);
      node = args[0].NodeSet.Next(out var anode) ? anode.Nav.Node : XPNodeRef.Invalid;
    }
    return new(node.Name.Local);
  }

  private ExecResult LibraryFuncNamespaceUri(ExecPathCtx ctx, ExecResult[] args)
  {
    var node = ctx.Nav.Node;
    if (args.Length > 0)
    {
      AssertArgType("namespace-uri", 0, XPValueType.NodeSet, in args[0]);
      node = args[0].NodeSet.Next(out var anode) ? anode.Nav.Node : XPNodeRef.Invalid;
    }
    return new(node.Name.NsUri);
  }

  private ExecResult LibraryFuncName(ExecPathCtx ctx, ExecResult[] args)
  {
    var node = ctx.Nav.Node;
    if (args.Length > 0)
    {
      AssertArgType("name", 0, XPValueType.NodeSet, in args[0]);
      node = args[0].NodeSet.Next(out var anode) ? anode.Nav.Node : XPNodeRef.Invalid;
    }
    var name = node.Name;
    if (name.Prefix.Length > 0)
      return new($"{name.Prefix}:{name.Local}");
    return new(name.Local);
  }

  private ExecResult LibraryFuncString(ExecPathCtx ctx, ExecResult[] args)
  {
    if (args.Length > 0)
      return new(args[0].StringValue);
    return new(ExecValue.NodeStringValue(ctx.Nav.Node));
  }

  private ExecResult LibraryFuncConcat(ExecPathCtx ctx, ExecResult[] args) =>
    throw new NotImplementedException();
  private ExecResult LibraryFuncStartsWith(ExecPathCtx ctx, ExecResult[] args) =>
    new(args[0].StringValue.StartsWith(args[1].StringValue));
  private ExecResult LibraryFuncContains(ExecPathCtx ctx, ExecResult[] args) =>
    new(args[0].StringValue.Contains(args[1].StringValue));

  private ExecResult LibraryFuncSubstringBefore(ExecPathCtx ctx, ExecResult[] args)
  {
    var arg0 = args[0].StringValue;
    var idx = arg0.IndexOf(args[1].StringValue);
    return new(idx == -1 ? "" : arg0[..idx]);
  }

  private ExecResult LibraryFuncSubstringAfter(ExecPathCtx ctx, ExecResult[] args)
  {
    var arg0 = args[0].StringValue;
    var arg1 = args[1].StringValue;
    var idx = arg0.IndexOf(arg1);
    return new(idx == -1 ? "" : arg0[(idx + arg1.Length)..]);
  }

  private ExecResult LibraryFuncSubstring(ExecPathCtx ctx, ExecResult[] args)
  {
    var str = args[0].StringValue;
    var min = (long)(int)Math.Round(args[1].NumberValue - 1);
    var len = args.Length > 2 ? (long)(int)Math.Round(args[1].NumberValue) : str.Length;

    var imin = (int)Math.Clamp(min, 0, str.Length);
    var imax = (int)Math.Clamp(min + len, 0, str.Length);
    if (imin >= imax)
      return new("");
    return new(str[imin..imax]);
  }

  private ExecResult LibraryFuncStringLength(ExecPathCtx ctx, ExecResult[] args)
  {
    if (args.Length > 0)
      return new(args[0].StringValue.Length);
    return new(ExecValue.NodeStringValue(ctx.Nav.Node).Length);
  }

  private ExecResult LibraryFuncNormalizeSpace(ExecPathCtx ctx, ExecResult[] args)
  {
    var str = args.Length > 0
      ? args[0].StringValue
      : ExecValue.NodeStringValue(ctx.Nav.Node);

    var res = new char[str.Length];
    var len = 0;
    var nslen = 0;
    var hassp = true;
    for (var i = 0; i < str.Length; i++)
    {
      var c = str[i];
      var hadsp = hassp;
      if (hassp = char.IsWhiteSpace(c))
      {
        if (hadsp) continue;
        c = ' ';
      }
      res[len++] = c;
      if (!hassp) nslen = len;
    }
    return new(res.AsSpan(nslen));
  }

  private ExecResult LibraryFuncTranslate(ExecPathCtx ctx, ExecResult[] args) =>
    throw new NotSupportedException("translate not supported");
  private ExecResult LibraryFuncBoolean(ExecPathCtx ctx, ExecResult[] args) =>
    new(args[0].BoolValue);
  private ExecResult LibraryFuncNot(ExecPathCtx ctx, ExecResult[] args) =>
    new(!args[0].BoolValue);
  private ExecResult LibraryFuncTrue(ExecPathCtx ctx, ExecResult[] args) => new(true);
  private ExecResult LibraryFuncFalse(ExecPathCtx ctx, ExecResult[] args) => new(false);
  private ExecResult LibraryFuncLang(ExecPathCtx ctx, ExecResult[] args) =>
    throw new NotSupportedException("lang not supported");

  private ExecResult LibraryFuncNumber(ExecPathCtx ctx, ExecResult[] args)
  {
    if (args.Length > 0)
      return new(args[0].NumberValue);
    return new(ExecValue.NodeNumberValue(ctx.Nav.Node));
  }

  private ExecResult LibraryFuncSum(ExecPathCtx ctx, ExecResult[] args)
  {
    AssertArgType("sum", 0, XPValueType.NodeSet, in args[0]);
    var sum = 0.0;
    foreach (var node in args[0].NodeSet) {
      sum += ExecValue.NodeNumberValue(node);
    }
    return new(sum);
  }

  private ExecResult LibraryFuncFloor(ExecPathCtx ctx, ExecResult[] args) =>
    new(Math.Floor(args[0].NumberValue));
  private ExecResult LibraryFuncCeiling(ExecPathCtx ctx, ExecResult[] args) =>
    new(Math.Ceiling(args[0].NumberValue));
  private ExecResult LibraryFuncRound(ExecPathCtx ctx, ExecResult[] args) =>
    new(Math.Round(args[0].NumberValue));
}