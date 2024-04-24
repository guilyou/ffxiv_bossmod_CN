﻿namespace BossMod;

public enum Waymark : byte
{
    A, B, C, D, N1, N2, N3, N4, Count
}

// waymark positions in world; part of the world state structure
public class WaymarkState
{
    private BitMask _setMarkers;
    private readonly Vector3[] _positions = new Vector3[(int)Waymark.Count];

    public Vector3? this[int wm]
    {
        get => _setMarkers[wm] ? _positions[wm] : null;
        private set
        {
            _setMarkers[wm] = value != null;
            _positions[wm] = value ?? default;
        }
    }

    public Vector3? this[Waymark wm]
    {
        get => this[(int)wm];
        private set => this[(int)wm] = value;
    }

    public IEnumerable<WorldState.Operation> CompareToInitial()
    {
        foreach (var i in _setMarkers.SetBits())
            yield return new OpWaymarkChange() { ID = (Waymark)i, Pos = _positions[i] };
    }

    // implementation of operations
    public Event<OpWaymarkChange> Changed = new();
    public class OpWaymarkChange : WorldState.Operation
    {
        public Waymark ID;
        public Vector3? Pos;

        protected override void Exec(WorldState ws)
        {
            ws.Waymarks[ID] = Pos;
            ws.Waymarks.Changed.Fire(this);
        }

        public override void Write(ReplayRecorder.Output output)
        {
            if (Pos != null)
                WriteTag(output, "WAY+").Emit((byte)ID).Emit(Pos.Value);
            else
                WriteTag(output, "WAY-").Emit((byte)ID);
        }
    }
}
