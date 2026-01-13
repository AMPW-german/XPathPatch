
using System;
using XPP.Doc;
using XPP.Utils;

namespace XPP.Patch;

public class PatchLog
{
  public readonly PatchDomain Domain;
  private readonly AppendTree<PatchAction> actions;

  public ActionRef Root => new(this, actions.Root);

  public PatchLog(PatchDomain domain)
  {
    Domain = domain;
    actions = new(new()
    {
      Type = ActionType.Root,
      InVersion = 0,
      OutVersion = 0,
      Context = domain.Root.Id,
      Target = XPNodeId.Invalid,
      Source = XPNodeId.Invalid,
    });
  }

  public readonly struct ActionRef(
    PatchLog Log, AppendTree<PatchAction>.NodeRef treeNode)
  {
    public static readonly ActionRef Invalid =
      new(null, AppendTree<PatchAction>.NodeRef.Invalid);
    public readonly PatchLog Log = Log;
    private readonly AppendTree<PatchAction>.NodeRef treeNode = treeNode;

    public bool Valid => Log != null && treeNode.Valid;
    public ActionRef Parent => new(Log, treeNode.Parent);
    public ActionRef NextSibling => new(Log, treeNode.NextSibling);
    public ActionRef FirstChild => new(Log, treeNode.FirstChild);

    private XPNodeRef MkNodeRef(XPNodeId id) =>
      Valid && id.Valid ? new(Log.Domain.Doc, id) : XPNodeRef.Invalid;

    public XPNodeRef Context => MkNodeRef(Action.Context);
    public XPNodeRef Target => MkNodeRef(Action.Target);
    public XPNodeRef Source => MkNodeRef(Action.Source);

    public ref readonly PatchAction Action => ref treeNode.Value;

    public ActionRef AddChild(
      ActionType type,
      XPNodeId? context = null, XPNodeId? target = null, XPNodeId? source = null,
      PatchPosition pos = default,
      string targetPath = null, Range targetResult = default,
      string sourcePath = null, Range sourceResult = default)
    {
      ref readonly var action = ref Action;
      return new(Log, treeNode.AddChild(new()
      {
        Type = type,
        InVersion = -1,
        OutVersion = -1,
        Context = context ?? action.Context,
        Target = target ?? action.Target,
        Source = source ?? XPNodeId.Invalid,
        Position = pos,
        TargetPath = targetPath,
        TargetResult = targetResult,
        SourcePath = sourcePath,
        SourceResult = sourceResult,
      }));
    }

    public ActionRef WithContext(XPNodeRef node) =>
      AddChild(ActionType.Context, context: node.Id);

    public void Start()
    {
      ref var action = ref treeNode.Value;
      if (action.InVersion != -1) throw new InvalidOperationException();
      action.InVersion = Log.Domain.Doc.Version;
      action.Context = Context.LatestVersion.Id;
    }

    public void Finish()
    {
      ref var action = ref treeNode.Value;
      if (action.InVersion == -1) throw new InvalidOperationException();
      if (action.OutVersion != -1) throw new InvalidOperationException();
      action.OutVersion = Log.Domain.Doc.Version;
    }

    public ChildEnumerator GetEnumerator() => new(Log, treeNode.GetEnumerator());
  }

  public struct ChildEnumerator(
    PatchLog log,
    AppendTree<PatchAction>.ChildEnumerator enumerator
  )
  {
    private readonly PatchLog log = log;
    private AppendTree<PatchAction>.ChildEnumerator enumerator = enumerator;

    public bool MoveNext() => enumerator.MoveNext();
    public ActionRef Current => new(log, enumerator.Current);
  }
}