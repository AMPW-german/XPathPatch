using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using XPP.Doc;
using XPP.Path;

namespace XPP.Tests;

[TestClass]
public partial class XPathTests : BaseTest
{
  public static string XPathTestDisplayName(MethodInfo method, object[] args) =>
    $"{method.Name}:{(args[0] as XPathEntry)?.Id ?? "ERROR"}";
}

public class XPathEntry
{
  [XmlAttribute("Id")] public string Id;
  [XmlAttribute("Expr")] public string Expr;
  [XmlArray("Tokenize"), XmlArrayItem("Tok")] public List<XPathTok> Tokens;
  [XmlElement("Parse")] public XPathAst Parsed;
  [XmlElement("Compile")] public XPathCExpr Compiled;

  public override string ToString() => Expr;
}

public partial class XPathTok
{
  [XmlAttribute("T")] public TokenType Type;
  [XmlAttribute("V")] public string Value;
}

public partial class XPathAst
{
  [XmlAttribute("T")] public AstType Type;
  [XmlAttribute("V")] public string Value;
  [XmlElement("L")] public XPathAst Left;
  [XmlElement("R")] public XPathAst Right;
}

public enum CExprType
{
  Invalid, Path, Constant, Negate, Logic, Compare, Math, Func, Variable, UserFunc,
}
public partial class XPathCExpr
{
  [XmlAttribute("T")] public CExprType Type;
  [XmlAttribute("Str")] public string String = "<MISSING>";
  [XmlAttribute("Num")] public double Number = double.NaN;
  [XmlAttribute("Op")] public TokenType Op = TokenType.Invalid;
  [XmlAttribute("Fn")] public LibraryFunc Func = LibraryFunc.Invalid;
  [XmlElement("Path")] public List<XPathCPath> Path;
  [XmlElement("L")] public XPathCExpr Left;
  [XmlElement("R")] public XPathCExpr Right;
  [XmlElement("Arg")] public List<XPathCExpr> Args;
}

public enum CPathType
{
  Invalid, Context, Root, Reverse, Dedupe, Union, Expr, NodeType, NameTest, Filter, Axis,
}
public partial class XPathCPath
{
  [XmlAttribute("T")] public CPathType Type;
  [XmlAttribute("Fwd")] public bool Forward = true;
  [XmlArray("UL"), XmlArrayItem("Path")] public List<XPathCPath> UnionL;
  [XmlArray("UR"), XmlArrayItem("Path")] public List<XPathCPath> UnionR;
  [XmlAttribute("Ax")] public AxisType Axis = AxisType.Invalid;
  [XmlAttribute("Pt")] public XPType PType = XPType.Element;
  [XmlAttribute("Nt")] public NodeType NodeType = NodeType.Invalid;
  [XmlAttribute("Ns")] public string Ns;
  [XmlAttribute("N")] public string Name;
  [XmlElement("Expr")] public XPathCExpr Expr;
}

public partial class XPathExecEntry
{
  [XmlAnyElement] public XmlElement Doc;
  [XmlElement("Test")] public List<XPathExecTest> Tests = [];
}

public partial class XPathExecTest
{
  [XmlAttribute("Id")] public string Id;
  [XmlAttribute("Path")] public string Path;
  [XmlElement("Bool", typeof(XPathBoolValue))]
  [XmlElement("Number", typeof(XPathNumberValue))]
  [XmlElement("String", typeof(XPathStringValue))]
  [XmlElement("Node", typeof(XPathNodeValue))]
  public List<XPathExecValue> Expected = [];

  public override string ToString() => Path;
}

public abstract partial class XPathExecValue { }

public class XPathBoolValue : XPathExecValue
{
  [XmlAttribute("Value")] public bool Value;
}
public class XPathNumberValue : XPathExecValue
{
  [XmlAttribute("Value")] public double Value;
}
public class XPathStringValue : XPathExecValue
{
  [XmlAttribute("Value")] public string Value;
}
public class XPathNodeValue : XPathExecValue
{
  [XmlAttribute("Path")] public string Path;

  public static implicit operator BaseTest.Path(XPathNodeValue match) => match.Path;
}