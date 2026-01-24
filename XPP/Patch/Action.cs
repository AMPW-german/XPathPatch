
using System;
using System.Collections.Generic;
using XPP.Doc;
using XPP.Path;

namespace XPP.Patch;

public class PatchAction
{
  public static PatchAction NewRoot(PatchDomain domain) =>
    new(domain, null, ActionType.Root)
    {
      Target = domain.Root,
      Context = domain.Root,
    };

  public PatchAction(PatchDomain domain, PatchAction parent, ActionType type)
  {
    Domain = domain;
    Parent = parent;
    Type = type;
    ChildIndex = parent?.Children.Count ?? 0;
    parent?.Children.Add(this);
  }

  public readonly PatchDomain Domain;

  public readonly PatchAction Parent;
  public readonly int ChildIndex;
  public readonly List<PatchAction> Children = [];

  public readonly ActionType Type;
  public int InVersion = -1;
  public int OutVersion = -1;

  public XPNodeRef Context = XPNodeRef.Invalid;
  public XPNodeRef Patch = XPNodeRef.Invalid;
  public XPNodeRef Target = XPNodeRef.Invalid;
  public XPNodeRef Source = XPNodeRef.Invalid;
  public PatchPosition Position;

  public Exception Error = null;

  // xpath query strings and outputs
  public string TargetPath;
  public string SourcePath;
  public readonly List<ExecValue> TargetResult = [];
  public readonly List<ExecValue> SourceResult = [];

  public XPDocument Doc => Domain.Doc;

  public PatchAction AddChild(
    ActionType type, XPNodeRef? context = null, XPNodeRef? patch = null,
    XPNodeRef? target = null, XPNodeRef? source = null, PatchPosition pos = default,
    string targetPath = null, string sourcePath = null)
  {
    return new(Domain, this, type)
    {
      InVersion = -1,
      OutVersion = -1,
      Context = context ?? Context,
      Patch = patch ?? Patch,
      Target = target ?? Target,
      Source = source ?? XPNodeRef.Invalid,
      Position = pos,
      TargetPath = targetPath,
      SourcePath = sourcePath,
    };
  }

  public bool Started => InVersion != -1;
  public void Start()
  {
    if (Started) throw new InvalidOperationException();
    InVersion = Doc.Version;
    Context = Context.LatestVersion;

    if (TargetPath != null)
    {
      foreach (var res in Domain.ExecXPath(TargetPath, Context, Patch))
        TargetResult.Add(res);
    }
    if (SourcePath != null)
    {
      foreach (var res in Domain.ExecXPath(SourcePath, Context, Patch))
        SourceResult.Add(res);
    }
  }

  public bool Finished => OutVersion != -1;
  public void Finish()
  {
    if (!Started) throw new InvalidOperationException();
    if (Finished) throw new InvalidOperationException();
    OutVersion = Doc.Version;
  }
}