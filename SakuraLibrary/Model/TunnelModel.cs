﻿using System.Collections.Generic;

using SakuraLibrary.Proto;
using TunnelState = SakuraLibrary.Proto.Tunnel.Types.State;

namespace SakuraLibrary.Model
{
    public class TunnelModel : ModelBase
    {
        public readonly LauncherModel Launcher;

        public Tunnel Proto { get => _proto; set => Set(out _proto, value); }
        private Tunnel _proto;

        [SourceBinding(nameof(Proto))]
        public int Id => Proto.Id;

        [SourceBinding(nameof(Proto))]
        public int Node => Proto.Node;

        [SourceBinding(nameof(Proto))]
        public string Name => Proto.Name;

        [SourceBinding(nameof(Proto))]
        public string Type => Proto.Type.ToUpper();

        [SourceBinding(nameof(Proto))]
        public string Description => Proto.Description;

        [SourceBinding(nameof(Proto))]
        public TunnelState State => Proto.State;

        [SourceBinding(nameof(Proto))]
        public bool Enabled
        {
            get => Proto.Enabled;
            set => Launcher.RPC.UpdateTunnel(new TunnelUpdate()
            {
                Action = TunnelUpdate.Types.Action.Update,
                Tunnel = new Tunnel()
                {
                    Id = Id,
                    Enabled = value,
                },
            });
        }

        [SourceBinding(nameof(Proto))]
        public string Note => Proto.Note;

        [SourceBinding(nameof(Note))]
        public bool NoteEmpty => string.IsNullOrEmpty(Note);

        public string NodeName { get => _nodeName; set => Set(out _nodeName, value); }
        private string _nodeName;

        public TunnelModel(Tunnel proto, LauncherModel launcher, Dictionary<int, string> nodes = null)
        {
            Proto = proto;
            Launcher = launcher;
            SetNodeName(nodes);
        }

        public void SetNodeName(Dictionary<int, string> nodes) => NodeName = nodes != null && nodes.ContainsKey(Node) ? nodes[Node] : "未知节点";
    }
}
