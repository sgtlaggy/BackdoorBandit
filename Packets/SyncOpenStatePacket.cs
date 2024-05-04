using LiteNetLib.Utils;

namespace BackdoorBandit
{
    public struct SyncOpenStatePacket : INetSerializable
    {
        public int netID;
        public string objectID;
        public int objectType;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(netID);
            writer.Put(objectID);
            writer.Put(objectType);
        }

        public void Deserialize(NetDataReader reader)
        {
            netID = reader.GetInt();
            objectID = reader.GetString();
            objectType = reader.GetInt();
        }
    }

}
