
using System;
using XPP.Doc;
using XPP.Path;
using XPP.Utils;

namespace XPP.Patch;

public class PatchLog
{
  public readonly PatchDomain Domain;
  private readonly AppendTree<PatchAction> actions;
  private readonly AppendList<ExecValue> pathResults = [];

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

    public PatchDomain Domain => Log.Domain;
    public XPDocument Doc => Log.Domain.Doc;

    public bool Valid => Log != null && treeNode.Valid;
    public ActionRef Parent => new(Log, treeNode.Parent);
    public ActionRef NextSibling => new(Log, treeNode.NextSibling);
    public ActionRef FirstChild => new(Log, treeNode.FirstChild);

    private XPNodeRef MkNodeRef(XPNodeId id) =>
      Valid && id.Valid ? new(Doc, id) : XPNodeRef.Invalid;

    public XPNodeRef Context => MkNodeRef(Action.Context);
    public XPNodeRef Target => MkNodeRef(Action.Target);
    public XPNodeRef Source => MkNodeRef(Action.Source);

    public AppendList<ExecValue>.RangeEnumerator TargetResult =>
      Log.pathResults[Action.TargetResult];
    public AppendList<ExecValue>.RangeEnumerator SourceResult =>
      Log.pathResults[Action.SourceResult];

    public void SetSourceResult(params Span<ExecValue> values) =>
      treeNode.Value.SourceResult = Log.pathResults.AddRange(values);
    public void SetSourceResult(Range range) =>
      treeNode.Value.SourceResult = range;

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

    public void Start()
    {
      ref var action = ref treeNode.Value;
      if (action.InVersion != -1) throw new InvalidOperationException();
      action.InVersion = Doc.Version;
      action.Context = Context.LatestVersion.Id;

      if (action.TargetPath is string targetPath)
      {
        var start = Log.pathResults.Length;
        foreach (var res in XPath.Exec(targetPath, Context))
          Log.pathResults.Add(res);
        action.TargetResult = start..Log.pathResults.Length;
      }
      if (action.SourcePath is string sourcePath)
      {
        var start = Log.pathResults.Length;
        foreach (var res in XPath.Exec(sourcePath, Context))
          Log.pathResults.Add(res);
        action.SourceResult = start..Log.pathResults.Length;
      }
    }

    public void Finish()
    {
      ref var action = ref treeNode.Value;
      if (action.InVersion == -1) throw new InvalidOperationException();
      if (action.OutVersion != -1) throw new InvalidOperationException();
      action.OutVersion = Doc.Version;
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