
using System;
using System.Collections.Generic;
using XPP.Doc;

namespace XPP.Path;

public interface IXPathUserContext
{
  public ExecResult GetVariable(string name, ExecPathCtx context);
  public ExecResult CallFunc(string name, ExecPathCtx context, ExecResult[] args);
}

public class ExecExprOpVariable(IXPathUserContext UserContext, string Name) : ExecExprOp()
{
  public readonly IXPathUserContext UserContext = UserContext;
  public readonly string Name = Name;

  public override ExecResult Value(ExecPathCtx context)
  {
    if (UserContext == null)
      throw new InvalidOperationException($"No UserContext for variable {Name}");
    return UserContext.GetVariable(Name, context);
  }
}

public class ExecExprOpUserFunc(
  IXPathUserContext UserContext, string Name, ExecExprOp[] Args) : ExecExprOp()
{
  public readonly IXPathUserContext UserContext = UserContext;
  public readonly string Name = Name;
  public readonly ExecExprOp[] Args = Args;
  private readonly ExecResult[] argValues = new ExecResult[Args.Length];

  public override ExecResult Value(ExecPathCtx context)
  {
    if (UserContext == null)
      throw new InvalidOperationException($"No UserContext for function {Name}");
    for (var i = 0; i < Args.Length; i++)
      argValues[i] = Args[i].Value(context);
    return UserContext.CallFunc(Name, context, argValues);
  }
}

public class ExecPathOpNodeList(List<XPNodeRef> Nodes) : ExecPathOp(null, true)
{
  public readonly List<XPNodeRef> Nodes = Nodes;
  private int index = 0;
  private readonly ExecCtxSet set = ExecCtxSet.Constant(Nodes.Count);

  public override void Init(XPNavigator navCtx)
  {
    base.Init(navCtx);
    index = 0;
  }

  public override bool Next(out ExecPathCtx next)
  {
    if (index >= Nodes.Count)
    {
      next = default;
      return false;
    }
    var curIndex = index++;
    next = new(Nodes[curIndex].Nav, set, curIndex);
    return true;
  }
}
