using EFT;
using LiteNetLib.Utils;

namespace BackdoorBandit
{
    public struct PlantTNTPacket : INetSerializable
    {
        public int netID;
        public string doorID;
        public int TNTTimer;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(netID);
            writer.Put(doorID);
            writer.Put(TNTTimer);
        }

        public void Deserialize(NetDataReader reader)
        {
            netID = reader.GetInt();
            doorID = reader.GetString();
            TNTTimer = reader.GetInt();
        }
    }

}
