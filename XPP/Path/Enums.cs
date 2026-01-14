
using System;

namespace XPP.Path;

public enum TokenType
{
  Invalid = -1,
  Unset = 0,

  // General Tokens
  // Non-op Precursor tokens
  Attr,  // @
  Axis,  // ::
  POpen, // (
  BOpen, // [
         // Rest of general tokens
  PClose, // )
  BClose, // ]
  Self,   // .
  Parent, // ..
  Comma,  // ,

  // Operator tokens
  // OperatorName tokens
  OpAnd, // and
  OpOr,  // or
  OpMod, // mod
  OpDiv, // div
         // Conditional based on previous
  OpMult, // *
  // Symbol operators
  OpSep,     // /
  OpSepDesc, // //
  OpUnion,   // |
  OpAdd,     // +
  OpSub,     // -
  OpEq,      // =
  OpNeq,     // !=
  OpLt,      // <
  OpLte,     // <=
  OpGt,      // >
  OpGte,     // >=

  // NameTest Tokens
  NtAny,   // *
  NtAnyNs, // NCName ':' '*'
  NtName,  // QName

  NodeType, // 'node' | 'text' | 'comment' | 'processing-instruction'

  FuncName, // QName - NodeType

  AxisName, // 'ancestor' | 'ancestor-or-self' | 'attribute' | 'child' | 'descendant' | 'descendant-or-self' |
            // 'following' | 'following-sibling' | 'namespace' | 'parent' | 'preceding' |
            // 'preceding-sibling' | 'self'

  String, // '[^']' | "[^"]"
  Number,

  VarRef, // '$' QName
}

public enum AstType
{
  Invalid = -1,
  Unset = 0,

  Root, // Tok:OpSep|OpSepDesc 0:RelativeLocationPath?
  Sep, // 0:Step Tok:OpSep|OpSepDesc 1:RelativeLocationPath
  Axis, // (Tok:AxisName OpAxis 0:NodeTest) | (Tok:Attr 0:NodeTest) | (Tok:Self|Parent)
  NodeTest, // Tok:NameTest | (Tok:NodeType POpen PClose)
  ProcType, // 'processing-instruction' POpen Tok:String PClose
  PathFilter, // 0:(Axis|NodeTest) Tok:BOpen 1:Expr BClose
  ExprFilter, // 0:Expr Tok:BOpen 1:Expr BClose
  Value, // Tok:VarRef|String|Number
  FuncCall, // Tok:FuncName POpen 0:(ArgList|Expr)? PClose
  ArgList, // 0:ArgList|Expr Tok:Comma 1:Expr

  // Binary ops = 0:Expr Tok:Op 1:Expr
  Union, // OpUnion
  BoolOp, // OpAnd OpOr
  CompareOp, // OpEq OpNeq OpLt OpLte OpGt OpGte
  MathOp, // OpAdd OpSub OpMult OpMod OpDiv

  Negate, // Tok:OpSub 0:Expr
}

public enum PathOpType
{
  Context, // start from context
  Root, // start from root
  Union, // start from union of [Paths]
  Axis, // walk axis from Parent
  NodeType, // filter nodes from Parent by NodeType
  NameTest, // filter nodes from Parent by [ns]:[name] (0:0 is *, 0:>0 is name, >0:>0 is ns:name, >0:0 is ns:*)
  Filter, // filter nodes from Parent by SubExpr
  Normalize, // sort by document order and Dedupe?
}

public enum ValOpType
{
  // Leaf val ops
  Number,
  String,
  Variable,
  Path, // get node-set from Path

  // Unary ops
  Negate,

  // Binary ops
  And, Or, // Boolean
  Eq, Neq, Lt, Lte, Gt, Gte, // Compare
  Add, Sub, Mult, Mod, Div, // Math

  // N-ary ops
  Func, // library function call
  UserFunc, // custom function call
}

public enum AxisType
{
  Invalid,
  Ancestor,         // ancestor
  AncestorOrSelf,   // ancestor-or-self
  Attribute,        // attribute
  Child,            // child
  Descendant,       // descendant
  DescendantOrSelf, // descendant-or-self
  Following,        // following
  FollowingSibling, // following-sibling
  Namespace,        // namespace
  Parent,           // parent
  Preceding,        // preceding
  PrecedingSibling, // preceding-sibling
  Self,             // self
}

public enum NodeType
{
  Invalid,
  Comment,               // comment
  Text,                  // text
  ProcessingInstruction, // processing-instruction
  Node,                  // node
}

public enum LibraryFunc
{
  Invalid,
  Last, // number last()
  Position, // number position()
  Count, // number count(node-set)
  Id, // node-set id(object)
  LocalName, // string local-name(node-set?)
  NamespaceUri, // string namespace-uri(node-set?)
  Name, // string name(node-set?)
  String, // string string(object?)
  Concat, // string concat(string, string, string*)
  StartsWith, // boolean starts-with(string, string)
  Contains, // boolean contains(string, string)
  SubstringBefore, // string substring-before(string, string)
  SubstringAfter, // string substring-after(string, string)
  Substring, // string substring(string, number, number?)
  StringLength, // number string-length(string?)
  NormalizeSpace, // string normalize-space(string?)
  Translate, // string translate(string, string, string)
  Boolean, // boolean boolean(object)
  Not, // boolean not(boolean)
  True, // boolean true()
  False, // boolean false()
  Lang, // boolean lang(string)
  Number, // number number(object?)
  Sum, // number sum(node-set)
  Floor, // number floor(number)
  Ceiling, // number ceiling(number)
  Round, // number round(number)
}

public enum XPValueType
{
  Bool, Number, String, NodeSet,
}

public partial class XPath
{
  // TokenType ranges
  public const TokenType MinPreNonOp = TokenType.Attr;
  public const TokenType MaxPreNonOp = TokenType.BOpen;
  public const TokenType MinOp = TokenType.OpAnd;
  public const TokenType MaxOp = TokenType.OpGte;
  public const TokenType MinOpName = TokenType.OpAnd;
  public const TokenType MaxOpName = TokenType.OpDiv;
  public const TokenType MinNt = TokenType.NtAny;
  public const TokenType MaxNt = TokenType.NtName;

  public static AxisType ParseAxisType(ReadOnlySpan<char> name) => name switch
  {
    "ancestor" => AxisType.Ancestor,
    "ancestor-or-self" => AxisType.AncestorOrSelf,
    "attribute" => AxisType.Attribute,
    "child" => AxisType.Child,
    "descendant" => AxisType.Descendant,
    "descendant-or-self" => AxisType.DescendantOrSelf,
    "following" => AxisType.Following,
    "following-sibling" => AxisType.FollowingSibling,
    "namespace" => AxisType.Namespace,
    "parent" => AxisType.Parent,
    "preceding" => AxisType.Preceding,
    "preceding-sibling" => AxisType.PrecedingSibling,
    "self" => AxisType.Self,
    _ => throw new InvalidOperationException($"Invalid AxisType {name}"),
  };

  public static NodeType ParseNodeType(ReadOnlySpan<char> name) => name switch
  {
    "comment" => NodeType.Comment,
    "text" => NodeType.Text,
    "processing-instruction" => NodeType.ProcessingInstruction,
    "node" => NodeType.Node,
    _ => throw new InvalidOperationException($"Invalid NodeType {name}"),
  };

  public static LibraryFunc ParseLibraryFunc(ReadOnlySpan<char> name) => name switch
  {
    "last" => LibraryFunc.Last,
    "position" => LibraryFunc.Position,
    "count" => LibraryFunc.Count,
    "id" => LibraryFunc.Id,
    "local-name" => LibraryFunc.LocalName,
    "namespace-uri" => LibraryFunc.NamespaceUri,
    "name" => LibraryFunc.Name,
    "string" => LibraryFunc.String,
    "concat" => LibraryFunc.Concat,
    "starts-with" => LibraryFunc.StartsWith,
    "contains" => LibraryFunc.Contains,
    "substring-before" => LibraryFunc.SubstringBefore,
    "substring-after" => LibraryFunc.SubstringAfter,
    "substring" => LibraryFunc.Substring,
    "string-length" => LibraryFunc.StringLength,
    "normalize-space" => LibraryFunc.NormalizeSpace,
    "translate" => LibraryFunc.Translate,
    "boolean" => LibraryFunc.Boolean,
    "not" => LibraryFunc.Not,
    "true" => LibraryFunc.True,
    "false" => LibraryFunc.False,
    "lang" => LibraryFunc.Lang,
    "number" => LibraryFunc.Number,
    "sum" => LibraryFunc.Sum,
    "floor" => LibraryFunc.Floor,
    "ceiling" => LibraryFunc.Ceiling,
    "round" => LibraryFunc.Round,
    _ => LibraryFunc.Invalid,
  };
}

public static partial class Extensions
{
  extension(TokenType type)
  {
    public ValOpType AsValOp => type switch
    {
      TokenType.OpAnd => ValOpType.And,
      TokenType.OpOr => ValOpType.Or,
      TokenType.OpMod => ValOpType.Mod,
      TokenType.OpDiv => ValOpType.Div,
      TokenType.OpMult => ValOpType.Mult,
      TokenType.OpAdd => ValOpType.Add,
      TokenType.OpSub => ValOpType.Sub,
      TokenType.OpEq => ValOpType.Eq,
      TokenType.OpNeq => ValOpType.Neq,
      TokenType.OpLt => ValOpType.Lt,
      TokenType.OpLte => ValOpType.Lte,
      TokenType.OpGt => ValOpType.Gt,
      TokenType.OpGte => ValOpType.Gte,
      _ => throw new InvalidOperationException($"{type}"),
    };
  }

  extension(AxisType axis)
  {
    public bool IsForward => axis switch
    {
      AxisType.Ancestor => false,
      AxisType.AncestorOrSelf => false,
      AxisType.Attribute => true,
      AxisType.Child => true,
      AxisType.Descendant => true,
      AxisType.DescendantOrSelf => true,
      AxisType.Following => true,
      AxisType.FollowingSibling => true,
      AxisType.Namespace => true,
      AxisType.Parent => false,
      AxisType.Preceding => false,
      AxisType.PrecedingSibling => false,
      AxisType.Self => true,
      _ => throw new InvalidOperationException($"{axis}"),
    };

    public bool CanDupe => axis switch
    {
      AxisType.Ancestor => true,
      AxisType.AncestorOrSelf => true,
      AxisType.Attribute => false,
      AxisType.Child => false,
      AxisType.Descendant => true,
      AxisType.DescendantOrSelf => true,
      AxisType.Following => true,
      AxisType.FollowingSibling => true,
      AxisType.Namespace => false,
      AxisType.Parent => true,
      AxisType.Preceding => true,
      AxisType.PrecedingSibling => true,
      AxisType.Self => false,
      _ => throw new InvalidOperationException($"{axis}"),
    };
  }

  extension(PathOpType type)
  {
    public bool IsRoot => type is PathOpType.Context or PathOpType.Root or PathOpType.Union;
  }
}