﻿namespace BossMod;

// this class represents parts of a world state that are interesting to boss modules
// it does not know anything about dalamud, so it can be used for UI test - there is a separate utility that updates it based on game state every frame
// world state is supposed to be modified using "operations" - this provides opportunity to listen and react to state changes
public class WorldState
{
    // state access
    public ulong QPF;
    public string GameVersion;
    public FrameState Frame;
    public ushort CurrentZone { get; private set; }
    public ushort CurrentCFCID { get; private set; }
    public readonly Dictionary<string, string> RSVEntries = [];
    public readonly WaymarkState Waymarks = new();
    public readonly ActorState Actors = new();
    public readonly PartyState Party;
    public readonly ClientState Client = new();
    public readonly PendingEffects PendingEffects = new();

    public DateTime CurrentTime => Frame.Timestamp;
    public DateTime FutureTime(float deltaSeconds) => Frame.Timestamp.AddSeconds(deltaSeconds);

    public WorldState(ulong qpf, string gameVersion)
    {
        QPF = qpf;
        GameVersion = gameVersion;
        Party = new(Actors);
    }

    // state modification
    public Event<Operation> Modified = new();
    public abstract class Operation
    {
        public DateTime Timestamp; // TODO: reconsider this field?..

        internal void Execute(WorldState ws)
        {
            Exec(ws);
            Timestamp = ws.CurrentTime;
        }

        protected ReplayRecorder.Output WriteTag(ReplayRecorder.Output output, string tag) => output.Entry(tag, Timestamp);

        protected abstract void Exec(WorldState ws);
        public abstract void Write(ReplayRecorder.Output output);
    }

    public void Execute(Operation op)
    {
        op.Execute(this);
        Modified.Fire(op);
    }

    // generate a set of operations that would turn default-constructed state into current state
    public IEnumerable<Operation> CompareToInitial()
    {
        if (CurrentTime != default)
            yield return new OpFrameStart() { Frame = Frame };
        if (CurrentZone != 0 || CurrentCFCID != 0)
            yield return new OpZoneChange() { Zone = CurrentZone, CFCID = CurrentCFCID };
        foreach (var (k, v) in RSVEntries)
            yield return new OpRSVData() { Key = k, Value = v };
        foreach (var o in Waymarks.CompareToInitial())
            yield return o;
        foreach (var o in Actors.CompareToInitial())
            yield return o;
        foreach (var o in Party.CompareToInitial())
            yield return o;
        foreach (var o in Client.CompareToInitial())
            yield return o;
    }

    // implementation of operations
    public Event<OpFrameStart> FrameStarted = new();
    public class OpFrameStart : Operation
    {
        public FrameState Frame;
        public TimeSpan PrevUpdateTime;
        public ulong GaugePayload;

        protected override void Exec(WorldState ws)
        {
            ws.Frame = Frame;
            ws.Client.Tick(Frame.Duration);
            ws.FrameStarted.Fire(this);
        }

        public override void Write(ReplayRecorder.Output output) => WriteTag(output, "FRAM")
            .Emit(PrevUpdateTime.TotalMilliseconds, "f3")
            .Emit()
            .Emit(GaugePayload, "X16")
            .Emit(Frame.QPC)
            .Emit(Frame.Index)
            .Emit(Frame.DurationRaw)
            .Emit(Frame.Duration)
            .Emit(Frame.TickSpeedMultiplier);
    }

    public Event<OpUserMarker> UserMarkerAdded = new();
    public class OpUserMarker : Operation
    {
        public string Text = "";

        protected override void Exec(WorldState ws) => ws.UserMarkerAdded.Fire(this);
        public override void Write(ReplayRecorder.Output output) => WriteTag(output, "UMRK").Emit(Text);
    }

    public Event<OpRSVData> RSVDataReceived = new();
    public class OpRSVData : Operation
    {
        public string Key = "";
        public string Value = "";

        protected override void Exec(WorldState ws)
        {
            Service.LuminaGameData?.Excel.RsvProvider.Add(Key, Value);
            ws.RSVEntries[Key] = Value;
            ws.RSVDataReceived.Fire(this);
        }

        public override void Write(ReplayRecorder.Output output) => WriteTag(output, "RSV ").Emit(Key).Emit(Value);
    }

    public Event<OpZoneChange> CurrentZoneChanged = new();
    public class OpZoneChange : Operation
    {
        public ushort Zone;
        public ushort CFCID;

        protected override void Exec(WorldState ws)
        {
            ws.CurrentZone = Zone;
            ws.CurrentCFCID = CFCID;
            ws.CurrentZoneChanged.Fire(this);
        }

        public override void Write(ReplayRecorder.Output output) => WriteTag(output, "ZONE").Emit(Zone).Emit(CFCID);
    }

    // global events
    public Event<OpDirectorUpdate> DirectorUpdate = new();
    public class OpDirectorUpdate : Operation
    {
        public uint DirectorID;
        public uint UpdateID;
        public uint Param1;
        public uint Param2;
        public uint Param3;
        public uint Param4;

        protected override void Exec(WorldState ws) => ws.DirectorUpdate.Fire(this);
        public override void Write(ReplayRecorder.Output output) => WriteTag(output, "DIRU").Emit(DirectorID, "X8").Emit(UpdateID, "X8").Emit(Param1, "X8").Emit(Param2, "X8").Emit(Param3, "X8").Emit(Param4, "X8");
    }

    public Event<OpEnvControl> EnvControl = new();
    public class OpEnvControl : Operation
    {
        public byte Index;
        public uint State;

        protected override void Exec(WorldState ws) => ws.EnvControl.Fire(this);
        public override void Write(ReplayRecorder.Output output) => WriteTag(output, "ENVC").Emit(Index, "X2").Emit(State, "X8");
    }
}
