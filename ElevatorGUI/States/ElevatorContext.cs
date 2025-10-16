using ElevatorGUI.States;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace ElevatorGUI
{
    public sealed class ElevatorContext
    {
        public event EventHandler<string>? StatusChanged;
        public event EventHandler<int>? FloorAligned;
        public event EventHandler<int>? CarMoved;        // Y pixel
        public event EventHandler<int>? TripCompletedMs; // elapsed ms

        public int FloorCount { get; }
        public IReadOnlyList<int> FloorY { get; } // index 1..N => Y coord
        public int CurrentFloor { get; set; }
        public int TargetFloor { get; set; }
        public int CarY { get; set; }
        public int SpeedPx { get; set; } = 6;

        private readonly Queue<(int floor, string source)> _queue = new();
        private readonly WinTimer _timer;
        private readonly Stopwatch _tripWatch = new();
        private IElevatorState _state;

        public ElevatorContext(int floorCount, IList<int> floorY, int startFloor, IElevatorState initial)
        {
            if (floorCount < 2) throw new ArgumentException("Requires at least 2 floors.");
            FloorCount = floorCount;
            FloorY = new List<int>(floorY);
            CurrentFloor = TargetFloor = startFloor;
            CarY = FloorY[startFloor];

            _state = initial;
            _timer = new WinTimer { Interval = 16 };
            _timer.Tick += (_, __) => _state.Tick(this);

            _state.Enter(this);
        }

        // API
        public void Request(int floor, string source)
        {
            if (floor < 1 || floor > FloorCount) return;
            _state.RequestFloor(this, floor, source);
        }

        public void StartTimer() => _timer.Start();
        public void StopTimer() => _timer.Stop();

        public void Enqueue(int floor, string source) => _queue.Enqueue((floor, source));
        public bool TryDequeue(out (int floor, string source) item) => _queue.TryDequeue(out item);

        public void SetState(IElevatorState next)
        {
            _state.Exit(this);
            _state = next;
            _state.Enter(this);
        }

        // Helpers for states
        public void BeginTripTo(int floor, string fromSource)
        {
            TargetFloor = floor;
            _tripWatch.Restart();
            StatusChanged?.Invoke(this, $"Moving ({fromSource}) → Floor {TargetFloor}");
            StartTimer();
        }

        public void StepTowardsTarget()
        {
            int targetY = FloorY[TargetFloor];
            if (CarY < targetY) CarY = Math.Min(targetY, CarY + SpeedPx);
            else CarY = Math.Max(targetY, CarY - SpeedPx);
            CarMoved?.Invoke(this, CarY);

            if (CarY == targetY)
            {
                _timer.Stop();
                _tripWatch.Stop();
                CurrentFloor = TargetFloor;
                FloorAligned?.Invoke(this, CurrentFloor);
                TripCompletedMs?.Invoke(this, (int)_tripWatch.ElapsedMilliseconds);
            }
        }

        public void Announce(string status) => StatusChanged?.Invoke(this, status);

        // NEW: event raiser for FloorAligned so states never invoke the event directly
        public void RaiseFloorAligned(int floor)
        {
            FloorAligned?.Invoke(this, floor);
        }
    }
}
