namespace CKeyViewer.Core
{
    /// <summary>
    /// 自由布局的图层组 —— 对应 jipper 的 <c>FmLayerGroup</c>。
    /// 节点通过 <see cref="KvFmNode.GroupId"/> 归组，组的 <see cref="Visible"/> 决定整组显隐。
    /// </summary>
    public sealed class KvFmLayerGroup
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public bool Visible { get; set; } = true;

        public KvFmLayerGroup Clone() => new KvFmLayerGroup { Id = Id, Name = Name, Visible = Visible };
    }
}
