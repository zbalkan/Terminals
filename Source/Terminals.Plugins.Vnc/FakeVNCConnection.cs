namespace Terminals.Connections
{
    internal class FakeVNCConnection : Connection
    {
        public override bool Connected { get { return true; } }

        public void SendSpecialKeys(VncSharpCore.SpecialKeys Keys)
        {
        }

        public override bool Connect()
        {
            return true;
        }
    }
}