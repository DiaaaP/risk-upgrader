using System;
using UnityEngine.Networking;
using RiskUpgrader;

internal static class MultiplayerProtocolTests
{
    private static int passed;

    private static void Equal<T>(T expected, T actual, string name)
    {
        if (!object.Equals(expected, actual)) throw new Exception(name + ": " + actual);
        passed++;
    }

    private static T RoundTrip<T>(T source) where T : MessageBase, new()
    {
        NetworkWriter writer = new NetworkWriter();
        source.Serialize(writer);
        NetworkReader reader = new NetworkReader(writer.ToArray());
        T result = new T();
        result.Deserialize(reader);
        return result;
    }

    public static void Main()
    {
        HelloMessage hello = RoundTrip(new HelloMessage { protocolVersion = MultiplayerProtocol.Version });
        Equal(MultiplayerProtocol.Version, hello.protocolVersion, "hello version");

        UpgradeRequestMessage request = RoundTrip(new UpgradeRequestMessage
        {
            protocolVersion = MultiplayerProtocol.Version,
            requestId = 42u,
            networkUserNetId = 77u,
            inputItemIndex = 3,
            targetItemIndex = 9,
            inputCount = 5,
            outputCount = 2
        });
        Equal(MultiplayerProtocol.Version, request.protocolVersion, "request protocol");
        Equal(42u, request.requestId, "request id");
        Equal(77u, request.networkUserNetId, "network user");
        Equal(3, request.inputItemIndex, "input index");
        Equal(9, request.targetItemIndex, "target index");
        Equal(5, request.inputCount, "input count");
        Equal(2, request.outputCount, "output count");

        UpgradeResultMessage result = RoundTrip(new UpgradeResultMessage
        {
            requestId = 42u,
            accepted = true,
            success = false,
            roll = 0.75f,
            chance = 0.5f,
            targetItemIndex = 9,
            outputCount = 2,
            error = UpgradeError.None
        });
        Equal(42u, result.requestId, "result id");
        Equal(true, result.accepted, "accepted");
        Equal(false, result.success, "success");
        Equal(0.75f, result.roll, "roll");
        Equal(0.5f, result.chance, "chance");
        Equal(9, result.targetItemIndex, "result target");
        Equal(2, result.outputCount, "result count");
        Equal(UpgradeError.None, result.error, "result error");

        OperatorStateMessage state = RoundTrip(new OperatorStateMessage
        {
            protocolVersion = MultiplayerProtocol.Version,
            networkUserNetId = 77u,
            isOpen = true
        });
        Equal(MultiplayerProtocol.Version, state.protocolVersion, "state protocol");
        Equal(77u, state.networkUserNetId, "state user");
        Equal(true, state.isOpen, "state open");

        Console.WriteLine("PASS: " + passed + " protocol assertions");
    }
}
