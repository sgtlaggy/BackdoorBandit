using LiteNetLib.Utils;

namespace BackdoorBandit
{
    public struct SyncOpenStatePacket : INetSerializable
    {
        public string profileID;
        public string objectID;
        public int objectType;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(profileID);
            writer.Put(objectID);
            writer.Put(objectType);
        }

        public void Deserialize(NetDataReader reader)
        {
            profileID = reader.GetString();
            objectID = reader.GetString();
            objectType = reader.GetInt();
        }
    }

}
