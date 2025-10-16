using ElevatorGUI.States;

namespace ElevatorGUI
{
    public sealed class MovingState : IElevatorState
    {
        public string Name => "Moving";

        public void Enter(ElevatorContext ctx) { }
        public void Exit(ElevatorContext ctx) { }

        public void RequestFloor(ElevatorContext ctx, int floor, string source)
        {
            // queue while moving
            ctx.Enqueue(floor, source);
        }

        public void Tick(ElevatorContext ctx)
        {
            ctx.StepTowardsTarget();
            if (ctx.CarY == ctx.FloorY[ctx.TargetFloor])
            {
                ctx.Announce("Arrived – Doors opening");
                ctx.SetState(new DoorOpenState());
            }
        }
    }
}
