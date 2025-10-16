using System;
using System.Diagnostics; // for Stopwatch
using System.Windows.Forms;
// Avoid ambiguity vs System.Threading.Timer
using WinTimer = System.Windows.Forms.Timer;

namespace ElevatorGUI
{
    /// <summary>
    /// Two-floor elevator controller with animated movement.
    /// Publishes events for the GUI to subscribe (delegation).
    /// </summary>
    public sealed class ElevatorController
    {
        public int CurrentFloor { get; private set; } = 1;   // 1 or 2
        public int TargetFloor { get; private set; } = 1;
        public bool IsMoving { get; private set; }

        // Shaft geometry (pixels) provided by the form
        public int Floor1Y { get; set; }     // lower Y (bottom)
        public int Floor2Y { get; set; }     // upper Y (top)
        public int CarY { get; private set; }
        public int SpeedPx { get; set; } = 6; // pixels per tick

        // Events
        public event EventHandler<string>? StatusChanged;
        public event EventHandler<int>? FloorChanged;
        public event EventHandler<int>? Arrived;
        public event EventHandler<int>? CarMoved;        // continuous movement updates (Y px)
        public event EventHandler<int>? TripCompletedMs; // elapsed ms for a trip

        private readonly WinTimer _moveTimer;
        private readonly WinTimer _doorTimer;
        private readonly Stopwatch _tripWatch = new();

        public ElevatorController()
        {
            _moveTimer = new WinTimer { Interval = 16 };   // ~60 FPS
            _moveTimer.Tick += (_, __) => Step();

            _doorTimer = new WinTimer { Interval = 800 };  // door-open dwell
            _doorTimer.Tick += (_, __) =>
            {
                _doorTimer.Stop();
                RaiseStatus("Doors closing");
                IsMoving = false;
                RaiseStatus("Idle");
            };
        }

        public void InitializeAt(int floor)
        {
            CurrentFloor = floor;
            TargetFloor = floor;
            CarY = (floor == 1) ? Floor1Y : Floor2Y;
            FloorChanged?.Invoke(this, CurrentFloor);
            RaiseStatus("Idle");
            CarMoved?.Invoke(this, CarY);
        }

        public void GoToFloor(int floor, string source)
        {
            if (floor != 1 && floor != 2) return;
            if (IsMoving && floor == TargetFloor) return;

            TargetFloor = floor;
            IsMoving = true;
            _tripWatch.Restart();                   // start timing
            RaiseStatus($"Moving ({source}) → Floor {TargetFloor}");
            _moveTimer.Start();
        }

        private void Step()
        {
            int targetY = (TargetFloor == 1) ? Floor1Y : Floor2Y;

            if (CarY == targetY)
            {
                _moveTimer.Stop();
                _tripWatch.Stop();
                int elapsed = (int)_tripWatch.ElapsedMilliseconds;

                CurrentFloor = TargetFloor;
                FloorChanged?.Invoke(this, CurrentFloor);
                RaiseStatus("Arrived – Doors opening");
                Arrived?.Invoke(this, CurrentFloor);
                TripCompletedMs?.Invoke(this, elapsed);

                _doorTimer.Start();
                return;
            }

            // Move towards targetY
            if (CarY < targetY) CarY = Math.Min(targetY, CarY + SpeedPx);
            else CarY = Math.Max(targetY, CarY - SpeedPx);

            CarMoved?.Invoke(this, CarY);
        }

        private void RaiseStatus(string s) => StatusChanged?.Invoke(this, s);
    }
}
