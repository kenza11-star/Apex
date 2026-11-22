using System;

namespace ApexUrpLab
{
    // Portable control model. Ground contact and collisions belong to the Unity adapter.
    public sealed class DriveModel
    {
        private readonly float force, topSpeed;
        private float reverseHold;
        public float Speed { get; private set; }
        public DriveModel(float force, float topSpeed)
        {
            this.force = force;
            this.topSpeed = topSpeed;
        }

        public void Reset()
        {
            Speed = 0;
            reverseHold = 0;
        }

        public void StopOnImpact() => Speed *= .25f;

        public void Update(float gas, float brake, bool handbrake, float dt)
        {
            if (dt <= 0) return;
            gas = Math.Clamp(gas, 0, 1);
            brake = Math.Clamp(brake, 0, 1);
            if (brake > 0 && gas < .05f && Speed < .30f) reverseHold += dt;
            else reverseHold = 0;

            if (reverseHold > .32f)
                Speed = Math.Max(-8f, Speed - brake * 4.2f * dt);
            else if (brake > 0 && gas < .05f)
                Speed = Approach(Speed, 0, brake * 16f * dt);
            else
            {
                float fraction = Math.Clamp(Math.Max(0, Speed) / topSpeed, 0, 1);
                Speed += gas * force * (1 - MathF.Pow(fraction, 1.7f)) * dt;
            }

            Speed = Approach(Speed, 0, (.28f + .00055f * Speed * Speed + (handbrake ? 5.4f : 0)) * dt);
        }

        private static float Approach(float source, float goal, float distance) =>
            MathF.Abs(source - goal) <= distance ? goal : source + MathF.Sign(goal - source) * distance;
    }
}
