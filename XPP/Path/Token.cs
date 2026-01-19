
using System;

namespace XPP.Path;

public readonly struct Token(TokenType Type, string String)
{
  public readonly TokenType Type = Type;
  public readonly string String = String;
}

public class Tokenizer(string source)
{
  private readonly string source = source;
  private int offset;
  private TokenType prev;

  public bool EOF => offset >= source.Length;

  private void Advance(int length)
  {
    if (length > 0)
      offset += length;
  }

  private void WS()
  {
    var len = 0;
    WS(ref len);
    Advance(len);
  }

  private void WS(ref int len)
  {
    while (len+offset < source.Length && char.IsWhiteSpace(source[len+offset]))
      len++;
  }

  private ReadOnlySpan<char> Str(int len) => source.AsSpan(offset, len);

  private bool Take(TokenType typ, int length, out Token tok) =>
    Take(tok = new(typ, new(Str(length))));

  private bool Take(Token tok)
  {
    prev = tok.Type;
    Advance(tok.String.Length);
    return true;
  }

  private char At(int index)
  {
    index += offset;
    if (index >= source.Length)
      return default;
    return source[index];
  }

  public bool Next(out Token tok)
  {
    WS();
    var c0 = At(0);
    var c1 = At(1);

    return (c0, c1) switch
    {
      ('@', _) => Take(TokenType.Attr, 1, out tok),
      (':', ':') => Take(TokenType.Axis, 2, out tok),
      ('(', _) => Take(TokenType.POpen, 1, out tok),
      ('[', _) => Take(TokenType.BOpen, 1, out tok),
      (')', _) => Take(TokenType.PClose, 1, out tok),
      (']', _) => Take(TokenType.BClose, 1, out tok),
      ('.', '.') => Take(TokenType.Parent, 2, out tok),
      ('.', _) => Take(TokenType.Self, 1, out tok),
      (',', _) => Take(TokenType.Comma, 1, out tok),
      ('*', _) => TakeMult(out tok),
      ('/', '/') => Take(TokenType.OpSepDesc, 2, out tok),
      ('/', _) => Take(TokenType.OpSep, 1, out tok),
      ('|', _) => Take(TokenType.OpUnion, 1, out tok),
      ('+', _) => Take(TokenType.OpAdd, 1, out tok),
      ('-', _) => Take(TokenType.OpSub, 1, out tok),
      ('=', _) => Take(TokenType.OpEq, 1, out tok),
      ('!', '=') => Take(TokenType.OpNeq, 2, out tok),
      ('<', '=') => Take(TokenType.OpLte, 2, out tok),
      ('<', _) => Take(TokenType.OpLt, 1, out tok),
      ('>', '=') => Take(TokenType.OpGte, 2, out tok),
      ('>', _) => Take(TokenType.OpGt, 1, out tok),
      ('\'' or '"', _) => TakeString(out tok),
      ('.' or { IsDecDigit: true }, _) => TakeNumber(out tok),
      ('$', _) => TakeVar(out tok),
      ({ IsNameStart: true }, _) => TakeName(out tok),
      _ => Take(TokenType.Invalid, 1, out tok),
    };
  }

  private bool TakeMult(out Token tok)
  {
    if (prev != TokenType.Unset && !prev.IsPreNonOp && !prev.IsOp)
      return Take(TokenType.OpMult, 1, out tok);
    return Take(TokenType.NtAny, 1, out tok);
  }

  private bool TakeString(out Token tok)
  {
    var first = At(0);
    var len = 1;
    while (len + offset < source.Length && At(len) != first)
      len++;
    if (At(len) != first)
      return Take(TokenType.Invalid, len, out tok);
    return Take(TokenType.String, len + 1, out tok);
  }

  private bool TakeNumber(out Token tok)
  {
    var len = 1;
    if (At(0) == '.')
    {
      while (At(len).IsDecDigit)
        len++;
      if (len == 1)
        return Take(TokenType.Invalid, len, out tok);
    }
    else
    {
      while (At(len).IsDecDigit)
        len++;
      if (At(len) == '.')
        len++;
      while (At(len).IsDecDigit)
        len++;
    }
    return Take(TokenType.Number, len, out tok);
  }

  private bool TakeName(out Token tok)
  {
    var len = 0;
    AdvanceNCName(ref len);

    if (prev != TokenType.Unset && !prev.IsPreNonOp && !prev.IsOp)
      return TakeOpName(len, out tok);

    var nclen = len;
    var qlen = len + 1;
    if (At(len) == ':' && AdvanceNCName(ref qlen))
      len = qlen;
    else
      qlen = nclen;

    WS(ref len);

    if (At(len) == '(')
      return TakeNodeTypeOrFunc(qlen, out tok);

    if (At(len) == ':' && At(len + 1) == ':')
      return TakeAxisName(qlen, out tok);

    // otherwise this is a NameTest
    if (qlen == nclen && At(nclen) == ':' && At(nclen + 1) == '*')
      return Take(TokenType.NtAnyNs, nclen + 2, out tok);
    return Take(TokenType.NtName, qlen, out tok);
  }

  private bool TakeOpName(int len, out Token tok) => Take(Str(len) switch
  {
    "and" => TokenType.OpAnd,
    "or" => TokenType.OpOr,
    "mod" => TokenType.OpMod,
    "div" => TokenType.OpDiv,
    _ => TokenType.Invalid,
  }, len, out tok);

  private bool TakeNodeTypeOrFunc(int len, out Token tok)
  {
    if (Str(len) is "node" or "text" or "comment" or "processing-instruction")
      return Take(TokenType.NodeType, len, out tok);
    return Take(TokenType.FuncName, len, out tok);
  }

  private bool TakeAxisName(int len, out Token tok)
  {
    if (Str(len) is "ancestor" or "ancestor-or-self" or "attribute" or "child" or "descendant"
        or "descendant-or-self" or "following" or "following-sibling" or "namespace" or "parent"
        or "preceding" or "preceding-sibling" or "self")
      return Take(TokenType.AxisName, len, out tok);
    return Take(TokenType.Invalid, len, out tok);
  }

  private bool TakeVar(out Token tok)
  {
    var len = 1;
    if (!AdvanceNCName(ref len))
      return Take(TokenType.Invalid, len, out tok);

    if (At(len) != ':')
      return Take(TokenType.VarRef, len, out tok);

    if (!AdvanceNCName(ref len))
      return Take(TokenType.Invalid, len, out tok);

    return Take(TokenType.VarRef, len, out tok);
  }

  private bool AdvanceNCName(ref int len)
  {
    if (!At(len).IsNameStart)
      return false;
    len++;
    while (At(len).IsNameChar)
      len++;
    return true;
  }
}

public static partial class Extensions
{
  extension(char c)
  {
    public bool IsNameStart => c switch
    {
      >= 'A' and <= 'Z' => true,
      '_' => true,
      >= 'a' and <= 'z' => true,
      >= '\xC0' and <= '\xD6' => true,
      >= '\xD8' and <= '\xF6' => true,
      >= '\xF8' and <= '\x2FF' => true,
      >= '\x370' and <= '\x37D' => true,
      >= '\x37F' and <= '\x1FFF' => true,
      >= '\x200C' and <= '\x200D' => true,
      >= '\x2070' and <= '\x218F' => true,
      >= '\x2C00' and <= '\x2FEF' => true,
      >= '\x3001' and <= '\xD7FF' => true,
      >= '\xF900' and <= '\xFDCF' => true,
      >= '\xFDF0' and <= '\xFFFD' => true,
      _ => false,
    };

    public bool IsNameChar => c switch
    {
      { IsNameStart: true } => true,
      '-' => true,
      '.' => true,
      >= '0' and <= '9' => true,
      '\xB7' => true,
      >= '\x0300' and <= '\x036F' => true,
      >= '\x203F' and <= '\x2040' => true,
      _ => false,
    };

    public bool IsDecDigit => c >= '0' && c <= '9';
  }

  extension(TokenType typ)
  {
    public bool IsPreNonOp => typ >= XPath.MinPreNonOp && typ <= XPath.MaxPreNonOp;
    public bool IsOp => typ >= XPath.MinOp && typ <= XPath.MaxOp;
    public bool IsOpName => typ >= XPath.MinOpName && typ <= XPath.MaxOpName;
    public bool IsNt => typ >= XPath.MinNt && typ <= XPath.MaxNt;
  }
}