using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteNetLib.Utils;

namespace BackdoorBandit
{
    public struct PlantTNTPacket : INetSerializable
    {
        public string profileID;
        public string doorID;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(profileID);
            writer.Put(doorID);
        }

        public void Deserialize(NetDataReader reader)
        {
            profileID = reader.GetString();
            doorID = reader.GetString();
        }
    }

}
