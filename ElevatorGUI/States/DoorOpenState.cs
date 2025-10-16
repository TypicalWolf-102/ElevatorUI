using ElevatorGUI.States;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace ElevatorGUI
{
    public sealed class DoorOpenState : IElevatorState
    {
        public string Name => "DoorOpen";
        private WinTimer? _doorTimer;

        public void Enter(ElevatorContext ctx)
        {
            _doorTimer = new WinTimer { Interval = 800 };
            _doorTimer.Tick += (_, __) =>
            {
                _doorTimer!.Stop();
                ctx.Announce("Doors closing");

                if (ctx.TryDequeue(out var next))
                {
                    ctx.BeginTripTo(next.floor, next.source);
                    ctx.SetState(new MovingState());
                }
                else
                {
                    ctx.SetState(new IdleState());
                }
            };
            _doorTimer.Start();
        }

        public void Exit(ElevatorContext ctx) => _doorTimer?.Stop();

        public void RequestFloor(ElevatorContext ctx, int floor, string source)
        {
            ctx.Enqueue(floor, source);
        }

        public void Tick(ElevatorContext ctx) { }
    }
}
