using System;

namespace TH.Control.Movement
{
    public interface IJumpMotor
    {
        event Action OnJumpStarted;
        event Action OnLanded;

        bool IsGrounded { get; }
        bool IsJumping { get; }
        float VerticalVelocity { get; }

        float CoyoteTime { get; }
        float JumpBufferTime { get; }
        float LastGroundedTime { get; }
        float LastJumpPressedTime { get; }

        void RequestJump();
        bool HasPendingJumpRequest();
        bool CanStartJump();
        bool TryStartJump();
    }
}
