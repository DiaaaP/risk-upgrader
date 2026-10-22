using UnityEngine.Networking;

namespace RiskUpgrader
{
    internal static class MultiplayerProtocol
    {
        public const int Version = 1;
        public const short HelloRequestType = 29361;
        public const short HelloResponseType = 29362;
        public const short UpgradeRequestType = 29363;
        public const short UpgradeResultType = 29364;
        public const short OperatorStateType = 29365;
    }

    internal enum UpgradeError : byte
    {
        None,
        ProtocolMismatch,
        NotOwner,
        NoRun,
        NoInventory,
        InvalidItems,
        InvalidCount,
        TargetUnavailable,
        InsufficientItems,
        RateLimited
    }

    internal sealed class HelloMessage : MessageBase
    {
        public int protocolVersion;

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(protocolVersion);
        }

        public override void Deserialize(NetworkReader reader)
        {
            protocolVersion = reader.ReadInt32();
        }
    }

    internal sealed class UpgradeRequestMessage : MessageBase
    {
        public int protocolVersion;
        public uint requestId;
        public uint networkUserNetId;
        public int inputItemIndex;
        public int targetItemIndex;
        public int inputCount;
        public int outputCount;

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(protocolVersion);
            writer.Write(requestId);
            writer.Write(networkUserNetId);
            writer.Write(inputItemIndex);
            writer.Write(targetItemIndex);
            writer.Write(inputCount);
            writer.Write(outputCount);
        }

        public override void Deserialize(NetworkReader reader)
        {
            protocolVersion = reader.ReadInt32();
            requestId = reader.ReadUInt32();
            networkUserNetId = reader.ReadUInt32();
            inputItemIndex = reader.ReadInt32();
            targetItemIndex = reader.ReadInt32();
            inputCount = reader.ReadInt32();
            outputCount = reader.ReadInt32();
        }
    }

    internal sealed class UpgradeResultMessage : MessageBase
    {
        public uint requestId;
        public bool accepted;
        public bool success;
        public float roll;
        public float chance;
        public int targetItemIndex;
        public int outputCount;
        public UpgradeError error;

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(requestId);
            writer.Write(accepted);
            writer.Write(success);
            writer.Write(roll);
            writer.Write(chance);
            writer.Write(targetItemIndex);
            writer.Write(outputCount);
            writer.Write((byte)error);
        }

        public override void Deserialize(NetworkReader reader)
        {
            requestId = reader.ReadUInt32();
            accepted = reader.ReadBoolean();
            success = reader.ReadBoolean();
            roll = reader.ReadSingle();
            chance = reader.ReadSingle();
            targetItemIndex = reader.ReadInt32();
            outputCount = reader.ReadInt32();
            error = (UpgradeError)reader.ReadByte();
        }
    }

    internal sealed class OperatorStateMessage : MessageBase
    {
        public int protocolVersion;
        public uint networkUserNetId;
        public bool isOpen;

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(protocolVersion);
            writer.Write(networkUserNetId);
            writer.Write(isOpen);
        }

        public override void Deserialize(NetworkReader reader)
        {
            protocolVersion = reader.ReadInt32();
            networkUserNetId = reader.ReadUInt32();
            isOpen = reader.ReadBoolean();
        }
    }
}
