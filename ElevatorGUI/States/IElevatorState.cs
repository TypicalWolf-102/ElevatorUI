using System;

namespace ElevatorGUI.States
{
    public interface IElevatorState
    {
        void Enter(ElevatorContext ctx);
        void Exit(ElevatorContext ctx);
        void RequestFloor(ElevatorContext ctx, int floor, string source);
        void Tick(ElevatorContext ctx); // timer callback (animation/doors)
        string Name { get; }
    }
}
