using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace WeatherController
{
    [BepInPlugin("com.noms.weathercontroller", "Weather Controller", "3.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static ManualLogSource Log;

        private bool showUI = false;
        private Rect windowRect = new Rect(20, 100, 340, 750);

        // Weather parameters
        private float timeOfDay = 12f;
        private float conditions = 0f;
        private float cloudHeight = 2000f;
        private float windSpeed = 5f;
        private float windTurbulence = 0.1f;
        private float windHeading = 0f;
        private float moonPhase = 0.5f;

        // Dynamic weather system
        private bool dynamicWeatherEnabled = false;
        private float dynamicSpeed = 1f;  // 1 = normal, 2 = fast, 0.5 = slow
        private float dynamicIntensity = 0.5f;  // 0 = calm, 1 = extreme

        // Target values for smooth transitions
        private float targetConditions = 0f;
        private float targetCloudHeight = 2000f;
        private float targetWindSpeed = 5f;
        private float targetWindTurbulence = 0.1f;
        private float targetWindHeading = 0f;

        // Dynamic weather timing
        private float weatherChangeTimer = 0f;
        private float nextWeatherChangeTime = 60f;
        private float windChangeTimer = 0f;
        private float nextWindChangeTime = 30f;
        private System.Random rng = new System.Random();

        //Dynamic time

        private bool dynamicTimeEnabled = false;
        private float dynamicTime = 1f;  // 1 = normal, 2 = fast, 0.5 = slow

        // Weather trend (for natural progression)
        private int weatherTrend = 0;  // -1 = improving, 0 = stable, 1 = worsening

        // Condition names
        private string[] conditionNames = { "Clear", "Scattered", "Broken", "Overcast", "Storm" };

        // Found weather members
        private object weatherTarget;
        private Dictionary<string, MemberInfo> weatherMembers = new Dictionary<string, MemberInfo>();
        private Dictionary<string, MethodInfo> weatherMethods = new Dictionary<string, MethodInfo>();
        private Dictionary<string, object> memberTargets = new Dictionary<string, object>();

        private bool initialized = false;
        private string statusMessage = "";
        private int foundCount = 0;

        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle statusStyle;
        private GUIStyle headerStyle;
        private GUIStyle smallButtonStyle;
        private GUIStyle toggleStyle;
        private bool stylesInitialized = false;

        private Vector2 scrollPosition = Vector2.zero;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("Weather Controller v3.0.0 loaded");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6))
            {
                showUI = !showUI;
                if (showUI && !initialized)
                {
                    SearchWeatherSystem();
                }
            }
            // Dynamic weather and time update
            if (initialized)
            {
                if (dynamicTimeEnabled)
                {
                    UpdateDynamicTime();
                }
                if (weatherTarget != null)
                {
                    if (dynamicWeatherEnabled)
                    {
                        UpdateDynamicWeather();
                    }
                }
            }
        }

        private void UpdateDynamicWeather()
        {
            float dt = Time.deltaTime * dynamicSpeed;

            // Update weather change timer
            weatherChangeTimer += dt;
            if (weatherChangeTimer >= nextWeatherChangeTime)
            {
                weatherChangeTimer = 0f;
                PickNewWeatherTarget();
                nextWeatherChangeTime = 30f + (float)rng.NextDouble() * 90f;  // 30-120 seconds
            }

            // Update wind change timer (wind changes more frequently)
            windChangeTimer += dt;
            if (windChangeTimer >= nextWindChangeTime)
            {
                windChangeTimer = 0f;
                PickNewWindTarget();
                nextWindChangeTime = 15f + (float)rng.NextDouble() * 45f;  // 15-60 seconds
            }

            // Smoothly interpolate current values toward targets
            float weatherLerpSpeed = 0.02f * dynamicSpeed;
            float windLerpSpeed = 0.05f * dynamicSpeed;

            conditions = Mathf.Lerp(conditions, targetConditions, weatherLerpSpeed * Time.deltaTime * 60f);
            cloudHeight = Mathf.Lerp(cloudHeight, targetCloudHeight, weatherLerpSpeed * Time.deltaTime * 60f);
            windSpeed = Mathf.Lerp(windSpeed, targetWindSpeed, windLerpSpeed * Time.deltaTime * 60f);
            windTurbulence = Mathf.Lerp(windTurbulence, targetWindTurbulence, windLerpSpeed * Time.deltaTime * 60f);

            // Wind heading interpolation (handle wrap-around)
            float headingDiff = Mathf.DeltaAngle(windHeading, targetWindHeading);
            windHeading = Mathf.MoveTowardsAngle(windHeading, targetWindHeading, Mathf.Abs(headingDiff) * windLerpSpeed * Time.deltaTime * 60f);
            if (windHeading < 0) windHeading += 360f;
            if (windHeading >= 360f) windHeading -= 360f;

            // Apply weather changes periodically (not every frame)
            if (Time.frameCount % 30 == 0)  // Every ~0.5 seconds at 60fps
            {
                ApplyWeatherSilent();
            }
        }

        private void UpdateDynamicTime()
        {
            timeOfDay += (Time.deltaTime * (60 * dynamicTime) / 3600f); // A dynamic time of 1 = 24 min per in game 24 hour cycle, 2 = 12 min, 3 = 6, etc...
            if (timeOfDay >= 24f) timeOfDay -= 24f;
            SetMemberValue("timeOfDay", timeOfDay, false);
            CallMethod("SetTimeOfDay", false, timeOfDay);
        }
        private void PickNewWeatherTarget()
        {
            // Determine weather trend
            if (rng.NextDouble() < 0.3)  // 30% chance to change trend
            {
                weatherTrend = rng.Next(-1, 2);  // -1, 0, or 1
            }

            // Calculate new target based on trend and intensity
            float maxCondition = 0.3f + dynamicIntensity * 0.7f;  // 0.3-1.0 based on intensity
            float conditionChange = (float)(rng.NextDouble() * 0.3 - 0.15);  // -0.15 to +0.15
            conditionChange += weatherTrend * 0.1f;  // Add trend influence

            targetConditions = Mathf.Clamp(conditions + conditionChange, 0f, maxCondition);

            // Cloud height inversely related to conditions
            targetCloudHeight = Mathf.Lerp(4000f, 600f, targetConditions) + (float)(rng.NextDouble() * 1000 - 500);
            targetCloudHeight = Mathf.Clamp(targetCloudHeight, 400f, 6000f);

            // Turbulence increases with conditions
            targetWindTurbulence = targetConditions * 0.4f + (float)rng.NextDouble() * 0.1f;

            Log.LogInfo($"Dynamic Weather: New target conditions={targetConditions:F2}, cloudHeight={targetCloudHeight:F0}, trend={weatherTrend}");
        }

        private void PickNewWindTarget()
        {
            // Wind speed based on conditions and intensity
            float baseWind = 2f + conditions * 15f;  // 2-17 m/s based on conditions
            float windVariation = (float)(rng.NextDouble() * 8 - 4);  // -4 to +4
            targetWindSpeed = Mathf.Clamp(baseWind + windVariation, 0f, 25f * dynamicIntensity + 5f);

            // Wind direction changes gradually
            float headingChange = (float)(rng.NextDouble() * 60 - 30);  // -30 to +30 degrees
            targetWindHeading = (windHeading + headingChange) % 360f;
            if (targetWindHeading < 0) targetWindHeading += 360f;

            Log.LogInfo($"Dynamic Weather: New target windSpeed={targetWindSpeed:F1}, windHeading={targetWindHeading:F0}");
        }

        private void ApplyWeatherSilent()
        {
            // Apply without logging every change
            if (weatherTarget == null) return;

            try
            {
                if (weatherMembers.ContainsKey("conditions"))
                {
                    var member = weatherMembers["conditions"];
                    var target = memberTargets.ContainsKey("conditions") ? memberTargets["conditions"] : weatherTarget;
                    if (member is PropertyInfo prop && prop.CanWrite)
                        prop.SetValue(target, conditions);
                }
                if (weatherMembers.ContainsKey("cloudHeight"))
                {
                    var member = weatherMembers["cloudHeight"];
                    var target = memberTargets.ContainsKey("cloudHeight") ? memberTargets["cloudHeight"] : weatherTarget;
                    if (member is PropertyInfo prop && prop.CanWrite)
                        prop.SetValue(target, cloudHeight);
                }
                if (weatherMembers.ContainsKey("windSpeed"))
                {
                    var member = weatherMembers["windSpeed"];
                    var target = memberTargets.ContainsKey("windSpeed") ? memberTargets["windSpeed"] : weatherTarget;
                    if (member is PropertyInfo prop && prop.CanWrite)
                        prop.SetValue(target, windSpeed);
                }
                if (weatherMembers.ContainsKey("windTurbulence"))
                {
                    var member = weatherMembers["windTurbulence"];
                    var target = memberTargets.ContainsKey("windTurbulence") ? memberTargets["windTurbulence"] : weatherTarget;
                    if (member is PropertyInfo prop && prop.CanWrite)
                        prop.SetValue(target, windTurbulence);
                }
                if (weatherMembers.ContainsKey("windHeading"))
                {
                    var member = weatherMembers["windHeading"];
                    var target = memberTargets.ContainsKey("windHeading") ? memberTargets["windHeading"] : weatherTarget;
                    if (member is PropertyInfo prop && prop.CanWrite)
                        prop.SetValue(target, windHeading);
                }

                if (weatherMethods.ContainsKey("SetWindHeading"))
                    weatherMethods["SetWindHeading"].Invoke(weatherTarget, new object[] { windHeading });
            }
            catch { }
        }

        private void StartDynamicWeather()
        {
            // Initialize targets to current values
            targetConditions = conditions;
            targetCloudHeight = cloudHeight;
            targetWindSpeed = windSpeed;
            targetWindTurbulence = windTurbulence;
            targetWindHeading = windHeading;

            weatherChangeTimer = 0f;
            windChangeTimer = 0f;
            nextWeatherChangeTime = 10f + (float)rng.NextDouble() * 20f;  // First change in 10-30 seconds
            nextWindChangeTime = 5f + (float)rng.NextDouble() * 10f;

            weatherTrend = 0;

            Log.LogInfo("Dynamic Weather started");
        }

        private void SearchWeatherSystem()
        {
            try
            {
                statusMessage = "Searching...";
                foundCount = 0;
                weatherMembers.Clear();
                weatherMethods.Clear();
                memberTargets.Clear();
                Log.LogInfo("=== WEATHER CONTROLLER v3.0 SEARCH ===");

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var bindFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

                string[] targetProps = {
                    "NetworktimeOfDay", "NetworkcloudHeight", "NetworkwindSpeed",
                    "NetworkwindTurbulence", "NetworkwindHeading", "NetworkmoonPhase",
                    "Networkconditions"
                };

                string[] targetMethods = {
                    "SetTimeOfDay", "SetWindHeading", "SetMoonPhase"
                };

                foreach (var assembly in assemblies)
                {
                    if (!assembly.FullName.Contains("Assembly-CSharp")) continue;

                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name != "LevelInfo") continue;

                            object instance = null;
                            try { instance = FindObjectOfType(type); } catch { }

                            if (instance == null) continue;

                            Log.LogInfo($"Found LevelInfo instance");
                            weatherTarget = instance;

                            foreach (var prop in type.GetProperties(bindFlags))
                            {
                                foreach (var target in targetProps)
                                {
                                    if (prop.Name == target)
                                    {
                                        string key = GetMemberKey(prop.Name);
                                        weatherMembers[key] = prop;
                                        memberTargets[key] = instance;
                                        foundCount++;
                                        Log.LogInfo($"Found: {prop.Name} -> {key}");
                                        ReadCurrentValue(key, prop, instance);
                                    }
                                }
                            }

                            foreach (var method in type.GetMethods(bindFlags))
                            {
                                foreach (var target in targetMethods)
                                {
                                    if (method.Name == target)
                                    {
                                        weatherMethods[method.Name] = method;
                                        Log.LogInfo($"Found method: {method.Name}");
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                initialized = true;
                statusMessage = $"Ready ({foundCount} found)";
                Log.LogInfo($"Search complete: {foundCount} members, {weatherMethods.Count} methods");
            }
            catch (Exception e)
            {
                statusMessage = $"Error: {e.Message}";
                Log.LogError($"Search error: {e}");
            }
        }

        private string GetMemberKey(string name)
        {
            name = name.ToLower().Replace("network", "");
            if (name.Contains("timeofday")) return "timeOfDay";
            if (name.Contains("cloudheight")) return "cloudHeight";
            if (name.Contains("windspeed")) return "windSpeed";
            if (name.Contains("windturbulence")) return "windTurbulence";
            if (name.Contains("windheading")) return "windHeading";
            if (name.Contains("moonphase")) return "moonPhase";
            if (name.Contains("conditions")) return "conditions";
            return name;
        }

        private void ReadCurrentValue(string key, PropertyInfo prop, object instance)
        {
            try
            {
                if (!prop.CanRead) return;
                var value = prop.GetValue(instance);
                switch (key)
                {
                    case "timeOfDay": timeOfDay = Convert.ToSingle(value); break;
                    case "cloudHeight": cloudHeight = Convert.ToSingle(value); break;
                    case "windSpeed": windSpeed = Convert.ToSingle(value); break;
                    case "windTurbulence": windTurbulence = Convert.ToSingle(value); break;
                    case "windHeading": windHeading = Convert.ToSingle(value); break;
                    case "moonPhase": moonPhase = Convert.ToSingle(value); break;
                    case "conditions": conditions = Convert.ToSingle(value); break;
                }
                Log.LogInfo($"  Current {key} = {value}");
            }
            catch { }
        }

        private void SetMemberValue(string key, object value, bool echo = true)
        {
            if (!weatherMembers.ContainsKey(key)) return;

            try
            {
                var member = weatherMembers[key];
                var target = memberTargets.ContainsKey(key) ? memberTargets[key] : weatherTarget;

                if (member is PropertyInfo prop && prop.CanWrite)
                {
                    prop.SetValue(target, Convert.ChangeType(value, prop.PropertyType));
                    if (echo) { Log.LogInfo($"Set {key} = {value}"); }
                }
                else if (member is FieldInfo field)
                {
                    field.SetValue(target, Convert.ChangeType(value, field.FieldType));
                    if (echo) { Log.LogInfo($"Set {key} = {value}"); }
                }
            }
            catch (Exception e)
            {
                Log.LogWarning($"Failed to set {key}: {e.Message}");
            }
        }

        private void CallMethod(string name, bool echo, params object[] args)
        {
            if (!weatherMethods.ContainsKey(name) || weatherTarget == null) return;

            try
            {
                weatherMethods[name].Invoke(weatherTarget, args);
                if (echo) { Log.LogInfo($"Called {name}"); }
            }
            catch (Exception e)
            {
                Log.LogWarning($"Failed to call {name}: {e.Message}");
            }
        }

        private void ApplyAllWeather()
        {
            if (weatherTarget == null)
            {
                statusMessage = "No target!";
                return;
            }

            int changes = 0;

            if (weatherMembers.ContainsKey("timeOfDay")) { SetMemberValue("timeOfDay", timeOfDay); changes++; }
            if (weatherMembers.ContainsKey("cloudHeight")) { SetMemberValue("cloudHeight", cloudHeight); changes++; }
            if (weatherMembers.ContainsKey("windSpeed")) { SetMemberValue("windSpeed", windSpeed); changes++; }
            if (weatherMembers.ContainsKey("windTurbulence")) { SetMemberValue("windTurbulence", windTurbulence); changes++; }
            if (weatherMembers.ContainsKey("windHeading")) { SetMemberValue("windHeading", windHeading); changes++; }
            if (weatherMembers.ContainsKey("moonPhase")) { SetMemberValue("moonPhase", moonPhase); changes++; }
            if (weatherMembers.ContainsKey("conditions")) { SetMemberValue("conditions", conditions); changes++; }

            if (weatherMethods.ContainsKey("SetTimeOfDay")) CallMethod("SetTimeOfDay", true, timeOfDay);
            if (weatherMethods.ContainsKey("SetWindHeading")) CallMethod("SetWindHeading", true, windHeading);
            if (weatherMethods.ContainsKey("SetMoonPhase")) CallMethod("SetMoonPhase", true, moonPhase);

            statusMessage = $"Applied {changes} settings";
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            boxStyle = new GUIStyle(GUI.skin.box);
            Texture2D bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.12f, 0.12f, 0.18f, 0.97f));
            bgTex.Apply();
            boxStyle.normal.background = bgTex;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 12;

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);
            headerStyle.fontSize = 13;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleCenter;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 11;

            smallButtonStyle = new GUIStyle(GUI.skin.button);
            smallButtonStyle.fontSize = 10;

            statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.normal.textColor = Color.yellow;
            statusStyle.fontSize = 11;
            statusStyle.alignment = TextAnchor.MiddleCenter;

            toggleStyle = new GUIStyle(GUI.skin.toggle);
            toggleStyle.normal.textColor = Color.white;

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!showUI) return;

            InitStyles();
            windowRect = GUI.Window(9998, windowRect, DrawWindow, "Weather Controller v3.0", boxStyle);
        }

        private void DrawWindow(int windowID)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.BeginVertical();

            GUILayout.Label(statusMessage, statusStyle);
            GUILayout.Space(5);

            // === DYNAMIC WEATHER ===
            DrawSection("Dynamic Weather");

            bool wasEnabled = dynamicWeatherEnabled;
            dynamicWeatherEnabled = GUILayout.Toggle(dynamicWeatherEnabled, " Enable Dynamic Weather", labelStyle);
            if (dynamicWeatherEnabled && !wasEnabled)
            {
                StartDynamicWeather();
            }

            if (dynamicWeatherEnabled)
            {
                GUILayout.Label($"Speed: {dynamicSpeed:F1}x", labelStyle);
                dynamicSpeed = GUILayout.HorizontalSlider(dynamicSpeed, 0.2f, 5f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Slow", smallButtonStyle)) dynamicSpeed = 0.5f;
                if (GUILayout.Button("Normal", smallButtonStyle)) dynamicSpeed = 1f;
                if (GUILayout.Button("Fast", smallButtonStyle)) dynamicSpeed = 2f;
                if (GUILayout.Button("Rapid", smallButtonStyle)) dynamicSpeed = 4f;
                GUILayout.EndHorizontal();
                GUILayout.Space(3);
                GUILayout.Space(3);
                GUILayout.Label($"Intensity: {GetIntensityName(dynamicIntensity)}", labelStyle);
                dynamicIntensity = GUILayout.HorizontalSlider(dynamicIntensity, 0f, 1f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Calm", smallButtonStyle)) dynamicIntensity = 0.2f;
                if (GUILayout.Button("Moderate", smallButtonStyle)) dynamicIntensity = 0.5f;
                if (GUILayout.Button("Active", smallButtonStyle)) dynamicIntensity = 0.75f;
                if (GUILayout.Button("Extreme", smallButtonStyle)) dynamicIntensity = 1f;
                GUILayout.EndHorizontal();

                GUILayout.Space(3);
                string trendStr = weatherTrend < 0 ? "Improving" : (weatherTrend > 0 ? "Worsening" : "Stable");
                GUILayout.Label($"Current Trend: {trendStr}", labelStyle);
                GUILayout.Label($"Next change in: {Mathf.Max(0, nextWeatherChangeTime - weatherChangeTimer):F0}s", labelStyle);
            }

            // === TIME ===
            DrawSection("Time");
            GUILayout.Label($"Time of Day: {timeOfDay:F1}h ({FormatTime(timeOfDay)})", labelStyle);
            timeOfDay = GUILayout.HorizontalSlider(timeOfDay, 0f, 24f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Dawn", smallButtonStyle)) timeOfDay = 5f;
            if (GUILayout.Button("Noon", smallButtonStyle)) timeOfDay = 12f;
            if (GUILayout.Button("Dusk", smallButtonStyle)) timeOfDay = 18f;
            if (GUILayout.Button("Night", smallButtonStyle)) timeOfDay = 0f;
            GUILayout.EndHorizontal();

            // === Dynamic Time ===
            dynamicTimeEnabled = GUILayout.Toggle(dynamicTimeEnabled, "Enable Dynamic Time");
            if (dynamicTimeEnabled)
            {
                GUILayout.Label($"Speed (Time): {dynamicTime:F1}x", labelStyle);
                dynamicTime = GUILayout.HorizontalSlider(dynamicTime, 0.2f, 5f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Slow", smallButtonStyle)) dynamicTime = 0.5f;
                if (GUILayout.Button("Normal", smallButtonStyle)) dynamicTime = 1f;
                if (GUILayout.Button("Fast", smallButtonStyle)) dynamicTime = 1.5f;
                if (GUILayout.Button("Rapid", smallButtonStyle)) dynamicTime = 3f;
                GUILayout.Label($"24 hours = {24 * (3600 / (dynamicTime * 60)) / 60:F1} min", labelStyle);
                GUILayout.EndHorizontal();
            }

            // === CONDITIONS ===
            DrawSection("Sky");
            GUILayout.Label($"Conditions: {conditions:F2} ({GetConditionName(conditions)})", labelStyle);
            GUI.enabled = !dynamicWeatherEnabled;
            conditions = GUILayout.HorizontalSlider(conditions, 0f, 1f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", smallButtonStyle)) conditions = 0f;
            if (GUILayout.Button("Scattered", smallButtonStyle)) conditions = 0.25f;
            if (GUILayout.Button("Broken", smallButtonStyle)) conditions = 0.5f;
            if (GUILayout.Button("Overcast", smallButtonStyle)) conditions = 0.75f;
            if (GUILayout.Button("Storm", smallButtonStyle)) conditions = 1f;
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.Label($"Cloud Height: {cloudHeight:F0}m", labelStyle);
            cloudHeight = GUILayout.HorizontalSlider(cloudHeight, 200f, 8000f);
            GUI.enabled = true;

            // === WIND ===
            DrawSection("Wind");
            GUILayout.Label($"Speed: {windSpeed:F1} m/s ({windSpeed * 3.6f:F0} km/h)", labelStyle);
            GUI.enabled = !dynamicWeatherEnabled;
            windSpeed = GUILayout.HorizontalSlider(windSpeed, 0f, 30f);

            GUILayout.Label($"Turbulence: {windTurbulence * 100:F0}%", labelStyle);
            windTurbulence = GUILayout.HorizontalSlider(windTurbulence, 0f, 1f);

            GUILayout.Label($"Direction: {windHeading:F0}° ({GetCompassDir(windHeading)})", labelStyle);
            windHeading = GUILayout.HorizontalSlider(windHeading, 0f, 360f);
            GUI.enabled = true;

            // === MOON ===
            DrawSection("Moon");
            GUILayout.Label($"Moon Phase: {GetMoonPhaseName(moonPhase)}", labelStyle);
            moonPhase = GUILayout.HorizontalSlider(moonPhase, 0f, 1f);

            // === APPLY ===
            GUILayout.Space(10);
            GUI.enabled = !dynamicWeatherEnabled;
            if (GUILayout.Button("APPLY ALL", buttonStyle, GUILayout.Height(35)))
            {
                ApplyAllWeather();
            }
            GUI.enabled = true;

            // === PRESETS ===
            DrawSection("Presets");
            GUI.enabled = !dynamicWeatherEnabled;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Day", buttonStyle))
            {
                timeOfDay = 12f; conditions = 0f; cloudHeight = 4000f;
                windSpeed = 3f; windTurbulence = 0.05f;
                ApplyAllWeather();
            }
            if (GUILayout.Button("Overcast", buttonStyle))
            {
                timeOfDay = 14f; conditions = 0.7f; cloudHeight = 1200f;
                windSpeed = 8f; windTurbulence = 0.15f;
                ApplyAllWeather();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Stormy", buttonStyle))
            {
                timeOfDay = 15f; conditions = 1f; cloudHeight = 600f;
                windSpeed = 20f; windTurbulence = 0.4f;
                ApplyAllWeather();
            }
            if (GUILayout.Button("Dawn", buttonStyle))
            {
                timeOfDay = 5.5f; conditions = 0.15f; cloudHeight = 2500f;
                windSpeed = 2f; windTurbulence = 0.05f;
                ApplyAllWeather();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Dusk", buttonStyle))
            {
                timeOfDay = 18.5f; conditions = 0.2f; cloudHeight = 2000f;
                windSpeed = 4f; windTurbulence = 0.1f;
                ApplyAllWeather();
            }
            if (GUILayout.Button("Night", buttonStyle))
            {
                timeOfDay = 2f; conditions = 0f; cloudHeight = 3000f;
                windSpeed = 3f; moonPhase = 0.5f;
                ApplyAllWeather();
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            GUILayout.Space(10);
            if (GUILayout.Button("Refresh", buttonStyle))
            {
                initialized = false;
                weatherTarget = null;
                weatherMembers.Clear();
                weatherMethods.Clear();
                SearchWeatherSystem();
            }

            GUIStyle helpStyle = new GUIStyle(labelStyle);
            helpStyle.fontSize = 9;
            helpStyle.normal.textColor = Color.gray;
            GUILayout.Label("F6 = Toggle UI", helpStyle);

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private void DrawSection(string title)
        {
            GUILayout.Space(8);
            GUILayout.Label($"─── {title} ───", headerStyle);
            GUILayout.Space(3);
        }

        private string FormatTime(float hours)
        {
            int h = (int)hours;
            int m = (int)((hours - h) * 60);
            return $"{h:D2}:{m:D2}";
        }

        private string GetConditionName(float cond)
        {
            if (cond < 0.15f) return "Clear";
            if (cond < 0.35f) return "Scattered";
            if (cond < 0.55f) return "Broken";
            if (cond < 0.85f) return "Overcast";
            return "Storm";
        }

        private string GetIntensityName(float intensity)
        {
            if (intensity < 0.25f) return "Calm";
            if (intensity < 0.5f) return "Moderate";
            if (intensity < 0.75f) return "Active";
            return "Extreme";
        }

        private string GetCompassDir(float heading)
        {
            if (heading >= 337.5 || heading < 22.5) return "N";
            if (heading >= 22.5 && heading < 67.5) return "NE";
            if (heading >= 67.5 && heading < 112.5) return "E";
            if (heading >= 112.5 && heading < 157.5) return "SE";
            if (heading >= 157.5 && heading < 202.5) return "S";
            if (heading >= 202.5 && heading < 247.5) return "SW";
            if (heading >= 247.5 && heading < 292.5) return "W";
            return "NW";
        }

        private string GetMoonPhaseName(float phase)
        {
            if (phase < 0.125f) return "New Moon";
            if (phase < 0.25f) return "Waxing Crescent";
            if (phase < 0.375f) return "First Quarter";
            if (phase < 0.5f) return "Waxing Gibbous";
            if (phase < 0.625f) return "Full Moon";
            if (phase < 0.75f) return "Waning Gibbous";
            if (phase < 0.875f) return "Last Quarter";
            return "Waning Crescent";
        }
    }
}
