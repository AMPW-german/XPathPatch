
namespace XPP.XPatch;

public partial class XPath
{
  public enum PathEvalType
  {
    Context, // start from context node
    Root, // start from root
    Axis, // walk axis from Parent
    NodeType, // filter nodes from Parent by NodeType
    NameTest, // filter nodes from Parent by [ns]:[name] (0:0 is *, 0:>0 is name, >0:>0 is ns:name, >0:0 is ns:*)
    Filter, // filter nodes from Parent by SubExpr
    
  }
}