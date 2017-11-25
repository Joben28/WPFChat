using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimplePackets
{
    [Serializable]
    public class BasicPacket
    {
        public string Heading;
        public string Content;
    }
}
