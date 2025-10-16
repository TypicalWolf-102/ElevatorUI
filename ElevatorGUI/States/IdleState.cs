using ElevatorGUI.States;

namespace ElevatorGUI
{
    public sealed class IdleState : IElevatorState
    {
        public string Name => "Idle";

        public void Enter(ElevatorContext ctx) => ctx.Announce("Idle");
        public void Exit(ElevatorContext ctx) { }

        public void RequestFloor(ElevatorContext ctx, int floor, string source)
        {
            if (floor == ctx.CurrentFloor)
            {
                ctx.RaiseFloorAligned(ctx.CurrentFloor);   // fixed: raise via context
                ctx.Announce("Doors opening");
                ctx.SetState(new DoorOpenState());
                return;
            }

            ctx.BeginTripTo(floor, source);
            ctx.SetState(new MovingState());
        }

        public void Tick(ElevatorContext ctx) { }
    }
}
