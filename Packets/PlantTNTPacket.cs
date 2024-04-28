using LiteNetLib.Utils;

namespace BackdoorBandit
{
    public struct PlantTNTPacket : INetSerializable
    {
        public string profileID;
        public string doorID;
        public int TNTTimer;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(profileID);
            writer.Put(doorID);
            writer.Put(TNTTimer);
        }

        public void Deserialize(NetDataReader reader)
        {
            profileID = reader.GetString();
            doorID = reader.GetString();
            TNTTimer = reader.GetInt();
        }
    }

}
