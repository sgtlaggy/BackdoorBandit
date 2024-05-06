using LiteNetLib.Utils;

namespace BackdoorBandit
{
    public struct PlantC4Packet : INetSerializable
    {
        public int netID;
        public string doorID;
        public int C4Timer;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(netID);
            writer.Put(doorID);
            writer.Put(C4Timer);
        }

        public void Deserialize(NetDataReader reader)
        {
            netID = reader.GetInt();
            doorID = reader.GetString();
            C4Timer = reader.GetInt();
        }
    }

}
