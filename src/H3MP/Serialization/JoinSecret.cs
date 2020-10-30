using System;
using System.Net;
using H3MP.IO;
using H3MP.Models;

namespace H3MP.Serialization
{
	public class JoinSecretSerializer : ISerializer<JoinSecret>
    {
		private readonly ISerializer<Version> _version;
		private readonly ISerializer<IPEndPoint> _endPoint;
		private readonly ISerializer<Key32> _key;
		private readonly ISerializer<double> _tickDeltaTime;
		private readonly ISerializer<byte> _maxPlayers;

        public JoinSecretSerializer()
		{
			_version = SystemSerializers.Version;
			_endPoint = SystemSerializers.IPEndPoint;
			_key = CustomSerializers.Key32;
			_tickDeltaTime = PrimitiveSerializers.Double;
			_maxPlayers = PrimitiveSerializers.Byte;
        }

        public JoinSecret Deserialize(ref BitPackReader reader)
        {
			var version = _version.Deserialize(ref reader);
			var endPoint = _endPoint.Deserialize(ref reader);
			var key = _key.Deserialize(ref reader);
			var tickDeltaTime = _tickDeltaTime.Deserialize(ref reader);
			var maxPlayers = _maxPlayers.Deserialize(ref reader);

			return new JoinSecret(version, endPoint, key, tickDeltaTime, maxPlayers);
        }

        public void Serialize(ref BitPackWriter writer, JoinSecret value)
        {
            _version.Serialize(ref writer, value.Version);
			_endPoint.Serialize(ref writer, value.EndPoint);
			_key.Serialize(ref writer, value.Key);
			_tickDeltaTime.Serialize(ref writer, value.TickDeltaTime);
			_maxPlayers.Serialize(ref writer, value.MaxPlayers);
        }
    }
}
