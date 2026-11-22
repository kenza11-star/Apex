using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ApexUrpLab
{
    [DefaultExecutionOrder(-20)]
    public sealed class LabSession : MonoBehaviour
    {
        private readonly struct Vehicle
        {
            public readonly string Id, Label;
            public readonly float Engine, Top, Wheelbase;
            public Vehicle(string id, string label, float engine, float top, float wheelbase)
            { Id = id; Label = label; Engine = engine; Top = top; Wheelbase = wheelbase; }
        }

        private static readonly Vehicle[] Catalog = {
            new Vehicle("mclaren", "McLaren W1", 12.7f, 67f, 2.65f),
            new Vehicle("pagani", "Pagani Huayra", 11.8f, 63f, 2.80f),
            new Vehicle("lexus", "Lexus LC 500", 8.7f, 54f, 2.85f),
            new Vehicle("f1", "McLaren F1", 11.1f, 64f, 2.72f),
            new Vehicle("eb110", "Bugatti EB110", 10.2f, 61f, 2.55f),
            new Vehicle("murcielago", "Lamborghini Murciélago", 10.8f, 60f, 2.66f)
        };
        private const string Preference = "ApexUrpLab.";
        private Camera view;
        private LabWorld world;
        private LabCar player;
        private LapTimer clock;
        private int activeVehicle, quality, cameraMode;
        private bool forest, loadedForest, pause, trial;
        private float steer, throttle, brake, best, previousLap, offCourseTimer;
        private bool handbrake, applyChanges, snapView;
        private string error;
        private GUIStyle heading, button, telemetry;
        private Rect left, right, accelerate, stop, slide;

        private void Start()
        {
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = 1f / 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.simulateMouseWithTouches = true;
            Physics.queriesHitTriggers = false;
            forest = PlayerPrefs.GetInt(Preference + "forest", 1) != 0;
            activeVehicle = Mathf.Clamp(PlayerPrefs.GetInt(Preference + "car"), 0, Catalog.Length - 1);
            quality = Mathf.Clamp(PlayerPrefs.GetInt(Preference + "quality"), 0, 1);
            if (GraphicsSettings.currentRenderPipeline is not UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)
            {
                error = "URP belum aktif. Jalankan Tools → APEX → Prepare URP Migration Lab.";
                return;
            }
            var cameraObject = new GameObject("Chase and hood camera");
            view = cameraObject.AddComponent<Camera>();
            view.fieldOfView = 58;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.46f, .63f, .73f);
            view.farClipPlane = 1200;
            view.allowHDR = false;

            var sun = new GameObject("Mountain sunshine").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1, .938f, .841f);
            sun.intensity = 1.14f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42, 138, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.60f, .68f, .77f);
            RenderSettings.ambientEquatorColor = new Color(.43f, .48f, .43f);
            RenderSettings.ambientGroundColor = new Color(.23f, .27f, .23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = view.backgroundColor;
            RenderSettings.fogDensity = .0023f;
            Rebuild();
        }

        private void Rebuild()
        {
            if (player)
            {
                player.gameObject.SetActive(false);
                Destroy(player.gameObject);
            }
            try
            {
                if (!world || loadedForest != forest)
                {
                    if (world)
                    {
                        world.gameObject.SetActive(false);
                        Destroy(world.gameObject);
                    }
                    world = new GameObject(forest ? "Forest Ridge" : "Marina Circuit").AddComponent<LabWorld>();
                    world.Configure(forest, view);
                    loadedForest = forest;
                    Physics.SyncTransforms();
                }
                world.Detail = quality;
                var spec = Catalog[activeVehicle];
                player = new GameObject(spec.Label).AddComponent<LabCar>();
                player.Setup(LabWorld.Load<GameObject>("Cars/" + spec.Id), spec.Id, spec.Engine, spec.Top, spec.Wheelbase);
                clock = new LapTimer(world.Course);
                best = PlayerPrefs.GetFloat(BestKey());
                previousLap = 0;
                PutCarOnCourse(-7f);
                pause = false;
                PlayerPrefs.SetInt(Preference + "car", activeVehicle);
                PlayerPrefs.SetInt(Preference + "forest", forest ? 1 : 0);
                PlayerPrefs.SetInt(Preference + "quality", quality);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                error = exception.Message;
            }
        }

        private string BestKey() => Preference + (forest ? "forest." : "marina.") + Catalog[activeVehicle].Id;
        private void PutCarOnCourse(float at)
        {
            var frame = world.Course.At(at, -1.25f);
            player.ResetAt(LabWorld.UnityPoint(frame.Position), LabWorld.UnityPoint(frame.Forward));
            clock.Invalidate();
            snapView = true;
        }

        private void Update()
        {
            if (error != null || !player) return;
            if (applyChanges)
            {
                applyChanges = false;
                Rebuild();
            }
            if (Input.GetKeyDown(KeyCode.Escape)) pause = !pause;
            if (Input.GetKeyDown(KeyCode.R) && !pause) Recover();
            if (Input.GetKeyDown(KeyCode.C) && !pause) NextCamera();
            Layout();
            steer = throttle = brake = 0;
            handbrake = false;
            if (pause) return;
            steer = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            throttle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0;
            brake = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0;
            handbrake = Input.GetKey(KeyCode.Space);
            bool pressingLeft = false, pressingRight = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch finger = Input.GetTouch(i);
                if (finger.phase is TouchPhase.Ended or TouchPhase.Canceled) continue;
                EvaluatePointer(new Vector2(finger.position.x, Screen.height - finger.position.y),
                    ref pressingLeft, ref pressingRight);
            }
            if (Input.touchCount == 0 && Input.GetMouseButton(0))
                EvaluatePointer(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y),
                    ref pressingLeft, ref pressingRight);
            if (pressingLeft || pressingRight) steer = (pressingRight ? 1 : 0) - (pressingLeft ? 1 : 0);
        }

        private void EvaluatePointer(Vector2 point, ref bool pressingLeft, ref bool pressingRight)
        {
            if (left.Contains(point)) pressingLeft = true;
            if (right.Contains(point)) pressingRight = true;
            if (accelerate.Contains(point)) throttle = 1;
            if (stop.Contains(point)) brake = 1;
            if (slide.Contains(point)) handbrake = true;
        }

        private void FixedUpdate()
        {
            if (!player || pause || error != null) return;
            Vector3 before = player.transform.position;
            float dt = Time.fixedDeltaTime;
            player.Step(steer, throttle, brake, handbrake, dt);
            Vector3 after = player.transform.position;
            if (after.y < -10)
            {
                Recover();
                return;
            }
            if (!trial) return;
            offCourseTimer += dt;
            if (offCourseTimer >= .18f)
            {
                offCourseTimer = 0;
                world.Course.NearestArc(LabWorld.CorePoint(after), out float width);
                if (width > 13f)
                {
                    clock.Invalidate();
                    return;
                }
            }
            float lap = clock.Observe(LabWorld.CorePoint(before), LabWorld.CorePoint(after), dt);
            if (lap <= 0) return;
            previousLap = lap;
            if (best <= 0 || lap < best)
            {
                best = lap;
                PlayerPrefs.SetFloat(BestKey(), best);
                PlayerPrefs.Save();
            }
        }

        private void Recover()
        {
            float at = world.Course.NearestArc(LabWorld.CorePoint(player.transform.position), out _);
            PutCarOnCourse(at);
        }

        private void NextCamera()
        {
            cameraMode ^= 1;
            snapView = true;
        }

        private void LateUpdate()
        {
            if (!player || error != null) return;
            Transform pose = player.transform;
            Vector3 aim = pose.position + Vector3.up * 1.02f + pose.forward * 1.55f;
            Vector3 eye = cameraMode == 0 ? pose.position + Vector3.up * 2.36f - pose.forward * 6.7f
                : pose.position + Vector3.up * 1.09f + pose.forward * .65f;
            if (cameraMode == 0)
            {
                Vector3 arm = eye - aim;
                if (Physics.SphereCast(aim, .20f, arm.normalized, out RaycastHit wall, arm.magnitude, (1 << 8) | (1 << 9)))
                    eye = aim + arm.normalized * Mathf.Max(.32f, wall.distance - .12f);
            }
            else aim += pose.forward * 18f;
            float mix = snapView ? 1f : 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            view.transform.position = Vector3.Lerp(view.transform.position, eye, mix);
            view.transform.rotation = Quaternion.LookRotation(aim - view.transform.position, Vector3.up);
            snapView = false;
        }

        private void Layout()
        {
            Rect safe = Screen.safeArea;
            float u = Mathf.Min(safe.width / 880f, safe.height / 430f);
            float bottom = Screen.height - safe.yMin, pad = 15f * u, size = 81f * u, gap = 8f * u;
            left = new Rect(safe.xMin + pad, bottom - size - pad, size, size);
            right = new Rect(left.xMax + gap, left.y, size, size);
            accelerate = new Rect(safe.xMax - size - pad, left.y, size, size);
            stop = new Rect(accelerate.xMin - size - gap, left.y, size, size);
            slide = new Rect(accelerate.x, accelerate.y - size - gap, size, size);
        }

        private static string ClockText(float seconds) => seconds <= 0 ? "—" : $"{(int)seconds / 60:00}:{seconds % 60:00.000}";

        private void OnGUI()
        {
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                telemetry = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, wordWrap = true };
            }
            Layout();
            Rect safe = Screen.safeArea;
            float u = Mathf.Min(safe.width / 880f, safe.height / 430f);
            float top = Screen.height - safe.yMax;
            heading.fontSize = Mathf.Max(13, Mathf.RoundToInt(27 * u));
            telemetry.fontSize = Mathf.Max(11, Mathf.RoundToInt(15 * u));
            button.fontSize = Mathf.Max(11, Mathf.RoundToInt(16 * u));
            if (error != null)
            {
                GUI.Box(new Rect(safe.xMin + 10, top + 10, safe.width - 20, 130 * u), error);
                return;
            }
            if (!player) return;

            if (!pause)
            {
                GUI.Label(new Rect(safe.center.x - 90 * u, top + 8 * u, 180 * u, 43 * u),
                    $"{Mathf.RoundToInt(Mathf.Abs(player.Speed) * 3.6f)} km/j", heading);
                if (trial)
                    GUI.Label(new Rect(safe.center.x - 190 * u, top + 50 * u, 380 * u, 31 * u),
                        $"Lap {clock.LapCount}  |  {ClockText(clock.Elapsed)}  |  terbaik {ClockText(best)}", telemetry);
                if (previousLap > 0)
                    GUI.Label(new Rect(safe.center.x - 190 * u, top + 79 * u, 380 * u, 26 * u),
                        "Putaran terakhir  " + ClockText(previousLap), telemetry);
                if (GUI.Button(new Rect(safe.xMax - 94 * u, top + 10 * u, 80 * u, 45 * u), "Jeda", button)) pause = true;
                if (GUI.Button(new Rect(safe.xMax - 180 * u, top + 10 * u, 80 * u, 45 * u), "Kamera", button)) NextCamera();
                if (GUI.Button(new Rect(safe.xMin + 16 * u, top + 10 * u, 84 * u, 45 * u), "Pulihkan", button)) Recover();
                GUI.Box(left, GUIContent.none);
                GUI.Box(right, GUIContent.none);
                GUI.Box(accelerate, GUIContent.none);
                GUI.Box(stop, GUIContent.none);
                GUI.Box(slide, GUIContent.none);
                GUI.Label(left, "‹", heading);
                GUI.Label(right, "›", heading);
                GUI.Label(accelerate, "Gas", telemetry);
                GUI.Label(stop, "Rem / R", telemetry);
                GUI.Label(slide, "Rem tangan", telemetry);
                return;
            }

            float w = 438 * u, x = safe.center.x - w * .5f, y = top + (safe.height - 305 * u) * .5f;
            GUI.Box(new Rect(x, y, w, 305 * u), GUIContent.none);
            GUI.Label(new Rect(x, y + 7 * u, w, 43 * u), "APEX Racing • URP", heading);
            x += 18 * u;
            w -= 36 * u;
            y += 51 * u;
            Rect Row(int i) => new Rect(x, y + i * 47 * u, w, 42 * u);
            if (GUI.Button(Row(0), "Lanjut", button)) pause = false;
            if (GUI.Button(Row(1), "Lintasan  •  " + (forest ? "Forest Ridge" : "Marina Circuit"), button))
            { forest = !forest; applyChanges = true; }
            if (GUI.Button(Row(2), "Mobil  •  " + Catalog[activeVehicle].Label, button))
            { activeVehicle = (activeVehicle + 1) % Catalog.Length; applyChanges = true; }
            if (GUI.Button(Row(3), trial ? "Mode  •  Time Trial" : "Mode  •  Free Drive", button))
            { trial = !trial; applyChanges = true; }
            if (GUI.Button(Row(4), quality == 0 ? "Detail vegetasi  •  Low" : "Detail vegetasi  •  Medium", button))
            {
                quality ^= 1;
                world.Detail = quality;
                PlayerPrefs.SetInt(Preference + "quality", quality);
                PlayerPrefs.Save();
            }
        }
    }
}
