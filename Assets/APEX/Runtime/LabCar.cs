using System;
using System.Collections.Generic;
using UnityEngine;

namespace ApexUrpLab
{
    public sealed class LabCar : MonoBehaviour
    {
        private const int GroundMask = 1 << 8;
        private static readonly string[] WheelNames = { "WheelLF", "WheelRF", "WheelLR", "WheelRR" };
        private static readonly Vector3[] Probes = {
            new Vector3(-.80f, 0, 1.31f), new Vector3(.80f, 0, 1.31f),
            new Vector3(-.80f, 0,-1.31f), new Vector3(.80f, 0,-1.31f)
        };

        private CharacterController controller;
        private Transform visual;
        private readonly Dictionary<string, Transform> wheels = new();
        private readonly Vector3[] ground = new Vector3[4];
        private DriveModel model;
        private float yaw, steering, falling, spin, wheelbase;
        public float Speed => model.Speed;

        public void Setup(GameObject prefab, string id, float engine, float topSpeed, float wheelbase)
        {
            this.wheelbase = wheelbase;
            model = new DriveModel(engine, topSpeed);
            controller = gameObject.AddComponent<CharacterController>();
            controller.center = new Vector3(0, .58f, 0);
            controller.radius = .62f;
            controller.height = 1.23f;
            controller.slopeLimit = 40;
            controller.stepOffset = .14f;
            controller.skinWidth = .027f;
            controller.minMoveDistance = 0;
            visual = new GameObject("Road-aligned visuals").transform;
            visual.SetParent(transform, false);
            var car = Instantiate(prefab, visual, false);
            car.transform.localRotation = Quaternion.Euler(0, id == "murcielago" ? 0 : 180, 0);
            if (id == "murcielago") car.transform.localScale = Vector3.one * .92f;
            foreach (Transform node in car.GetComponentsInChildren<Transform>(true))
                if (Array.IndexOf(WheelNames, node.name) >= 0)
                    wheels[node.name] = node;
        }

        public void ResetAt(Vector3 at, Vector3 forward)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(at + Vector3.up * .06f, Quaternion.LookRotation(forward, Vector3.up));
            controller.enabled = true;
            yaw = transform.eulerAngles.y;
            steering = falling = spin = 0;
            model.Reset();
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
        }

        public void Step(float steer, float gas, float brake, bool handbrake, float dt)
        {
            model.Update(gas, brake, handbrake, dt);
            float turningLimit = Mathf.Lerp(.53f, .15f, Mathf.Clamp01(Mathf.Abs(Speed) / 52f));
            steering = Mathf.MoveTowards(steering, steer * turningLimit, dt * 2.4f);
            yaw += Mathf.Clamp(Speed * Mathf.Tan(steering) / wheelbase, -1.42f, 1.42f) * Mathf.Rad2Deg * dt;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Vector3 movement = transform.forward * (Speed * dt);
            Vector3 predicted = transform.position + movement;
            const float depth = 3.7f;
            Vector3 normal = Vector3.up;
            bool road = Physics.Raycast(predicted + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, depth, GroundMask) &&
                hit.normal.y > .66f && Mathf.Abs(hit.point.y - transform.position.y) < .66f;
            if (road)
            {
                movement.y = hit.point.y - transform.position.y - .037f;
                normal = hit.normal;
                falling = 0;
            }
            else
            {
                falling = Mathf.Max(-31f, falling - 18f * dt);
                movement.y = falling * dt;
            }
            CollisionFlags flags = controller.Move(movement);
            if ((flags & CollisionFlags.Sides) != 0 && movement.sqrMagnitude > .012f)
                model.StopOnImpact();
            UpdatePose(normal, dt);
        }

        private void UpdatePose(Vector3 normal, float dt)
        {
            int found = 0;
            for (int i = 0; i < Probes.Length; i++)
            {
                Vector3 point = transform.TransformPoint(Probes[i]);
                if (Physics.Raycast(point + Vector3.up, Vector3.down, out RaycastHit hit, 2.2f, GroundMask))
                {
                    ground[i] = hit.point;
                    found++;
                }
            }
            Vector3 center = transform.position;
            if (found == 4)
            {
                Vector3 along = (ground[0] + ground[1]) - (ground[2] + ground[3]);
                Vector3 across = (ground[1] + ground[3]) - (ground[0] + ground[2]);
                Vector3 up = Vector3.Cross(along, across).normalized;
                if (up.y > .70f) normal = up;
                for (int i = 0; i < ground.Length; i++) center.y += (ground[i].y - transform.position.y) * .25f;
            }
            visual.rotation = Quaternion.Slerp(visual.rotation,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, normal), normal), 1f - Mathf.Exp(-14f * dt));
            visual.position = Vector3.Lerp(visual.position, center + Vector3.up * .015f, 1f - Mathf.Exp(-18f * dt));
            spin = Mathf.Repeat(spin - Speed * dt * Mathf.Rad2Deg / .335f, 360);
            foreach (var pair in wheels)
                pair.Value.localRotation = Quaternion.Euler(spin,
                    pair.Key.EndsWith("F", StringComparison.Ordinal) ? steering * Mathf.Rad2Deg : 0f, 0);
        }
    }
}
