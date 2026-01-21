
using System.Collections.Generic;
using System.Xml.Serialization;
using XPP.Doc;

namespace XPP.Patch;

public class PatchOpDeserializer
{
  private readonly Dictionary<string, XmlSerializer> serializers = [];

  public PatchOpDeserializer(bool registerDefault = true)
  {
    if (registerDefault)
      RegisterDefault();
  }

  public IPatchOp Deserialize(XPNodeRef node)
  {
    if (node.Type != XPType.Element || node.Name.Prefix != "")
      return null;
    if (!serializers.TryGetValue(node.Name.Local, out var serializer))
      return null;
    return serializer.Deserialize(new XPDocReader(node)) as IPatchOp;
  }

  public void Register<T>(
    string name, XmlSerializer serializer = null
  ) where T : IPatchOp, new() =>
    serializers[name] = serializer ?? new(typeof(T), new XmlRootAttribute(name));

  public void RegisterDefault()
  {
    Register<CopyPatch>("Copy");
    Register<MergePatch>("Merge");
    Register<DeletePatch>("Delete");
    Register<IfPatch>("If");
    Register<IfAnyPatch>("IfAny");
    Register<IfNonePatch>("IfNone");
    Register<WithPatch>("With");
    Register<SetVarPatch>("SetVar");
  }
}

public interface IPatchOp
{
  public void Build(PatchAction parent, XPNodeRef node);
}

public abstract class PatchOp : IPatchOp
{
  [XmlAttribute("Path")] public string Path = ".";
  [XmlAttribute("From")] public string From = "$patch/node()";

  public abstract void Build(PatchAction parent, XPNodeRef node);
}

[XmlRoot("Patch")]
public class PatchFile : PatchOp
{
  // TODO: priority/ordering

  public override void Build(PatchAction parent, XPNodeRef node) =>
    parent.AddChild(ActionType.OpPatch, patch: node, target: node);
}

public class CopyPatch : PatchOp
{
  [XmlAttribute("Pos")] public PatchPosition Pos;

  public override void Build(PatchAction parent, XPNodeRef node) =>
    parent.AddChild(ActionType.OpCopy, patch: node,
      target: node, pos: Pos, targetPath: Path, sourcePath: From);
}

public class MergePatch : PatchOp
{
  public override void Build(PatchAction parent, XPNodeRef node) =>
    parent.AddChild(ActionType.OpMerge, patch: node,
      target: node, targetPath: Path, sourcePath: From);
}

public class DeletePatch : PatchOp
{
  public override void Build(PatchAction parent, XPNodeRef node) =>
    parent.AddChild(ActionType.OpDelete, patch: node, target: node, targetPath: Path);
}

public class IfPatch : PatchOp
{
  public override void Build(PatchAction parent, XPNodeRef node)
  {
    var action = parent.AddChild(
      ActionType.OpIf, patch: node, target: node, targetPath: Path);
    var child = node.FirstContent;
    while (child.Valid)
    {
      if (child.Type is XPType.Element && child.Name == new XPName("", "", "Any"))
        action.AddChild(ActionType.OpIfAny, target: child);
      else if (child.Type is XPType.Element && child.Name == new XPName("", "", "None"))
        action.AddChild(ActionType.OpIfNone, target: child);
      child = child.NextSibling;
    }
  }
}

public class IfAnyPatch : PatchOp
{
  public override void Build(PatchAction parent, XPNodeRef node) =>
    parent.AddChild(ActionType.OpIfAny, patch: node, target: node, targetPath: Path);
}

public class IfNonePatch : PatchOp
{
  public override void Build(PatchAction parent, XPNodeRef node) =>
    parent.AddChild(ActionType.OpIfNone, patch: node, target: node, targetPath: Path);
}

public class WithPatch : PatchOp
{
  public override void Build(PatchAction parent, XPNodeRef node) =>
    parent.AddChild(ActionType.OpWith, patch: node, target: node, targetPath: Path);
}

public class SetVarPatch : PatchOp
{
  [XmlAttribute("Name")]
  public string Name = "";

  public override void Build(PatchAction parent, XPNodeRef node)
  {
    var action = parent.AddChild(
      ActionType.OpSetVar, patch: node, target: node, targetPath: Path);
    action.SourceResult.Add(new(Name));
  }
}