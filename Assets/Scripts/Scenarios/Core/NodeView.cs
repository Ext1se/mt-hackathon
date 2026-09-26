using System.Collections.Generic;
using Game.Scenarios.Core.Data;

namespace Game.Scenarios.Core
{
    /// <summary>The current node with its text and options already resolved against the state.</summary>
    public sealed class NodeView
    {
        public NodeView(NodeData node, string text, IReadOnlyList<OptionView> options, int hubActionsLeft)
        {
            Node = node;
            Text = text;
            Options = options;
            HubActionsLeft = hubActionsLeft;
        }

        public NodeData Node { get; }
        public string Text { get; }
        public IReadOnlyList<OptionView> Options { get; }
        public int HubActionsLeft { get; }
        public bool IsHub => Node.Kind == NodeKinds.Hub;
        public bool IsRoam => Node.Kind == NodeKinds.Roam || (Node.Kind == NodeKinds.Hub && Node.Roam);
    }
}
