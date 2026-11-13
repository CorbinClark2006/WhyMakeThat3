using UnityEngine;
using UnityEngine.Rendering;

namespace Convoy
{
    public static class ConvoyNight
    {
        public static readonly Color FogColor = new Color32(69, 41, 63, 255);
        public static readonly Color WarmLight = new Color32(242, 236, 139, 255);
        public const float FogStart = 55f;
        public const float FogEnd = 185f;

        public static void Apply(ConvoyPrototype game)
        {
            Material sky = Resources.Load<Material>("ConvoyNightSky");
            if (sky == null)
                throw new System.InvalidOperationException("Missing Convoy night sky. Run Tools > Convoy > Apply Night Atmosphere.");
            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.40f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.34f, 0.40f);
            RenderSettings.ambientGroundColor = new Color(0.23f, 0.19f, 0.26f);
            RenderSettings.reflectionIntensity = 0.25f;
            foreach (Camera camera in game.GetComponentsInChildren<Camera>(true))
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = FogColor;
            }
            GameObject sun = GameObject.Find("Afternoon sun") ?? GameObject.Find("Palette moonlight");
            if (sun != null)
            {
                sun.name = "Palette moonlight";
                Light light = sun.GetComponent<Light>();
                light.color = new Color32(169, 178, 162, 255);
                light.intensity = 0.85f;
                light.transform.rotation = Quaternion.Euler(24f, 155f, 0f);
                light.shadows = LightShadows.Hard;
            }
            Transform cabin = game.van.Find("Cabin fill");
            if (cabin != null)
            {
                Light light = cabin.GetComponent<Light>();
                light.color = WarmLight;
                light.intensity = 2.5f;
                light.range = 8f;
                light.shadows = LightShadows.None;
            }
            if (game.van.Find("Night fixtures") != null) return;
            Transform fixtures = new GameObject("Night fixtures").transform;
            fixtures.SetParent(game.van, false);
            Material glow = Resources.Load<Material>("ConvoyLamp");
            if (glow == null)
                throw new System.InvalidOperationException("Missing Convoy lamp material.");
            for (int side = -1; side <= 1; side += 2)
            {
                PrototypeGeometry.Part("Warm ceiling strip", fixtures, new Vector3(side * 1.1f, 2.85f, -0.8f),
                    new Vector3(0.09f, 0.04f, 1.2f), glow, false);
                PrototypeGeometry.Part("Main headlamp", fixtures, new Vector3(side * 1.1f, 0.65f, 5.9f),
                    new Vector3(0.48f, 0.18f, 0.08f), glow, false);
            }
            AddLight("Cargo lamp", fixtures, new Vector3(0f, 2.55f, -1.9f), LightType.Point, 3f, 7f);
            AddLight("Main road headlights", fixtures, new Vector3(0f, 0.85f, 5.9f), LightType.Spot, 12f, 85f)
                .transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        }

        public static void AddPursuerLights(PursuerVan enemy, Bounds bounds)
        {
            Material glow = Resources.Load<Material>("ConvoyLamp");
            if (glow == null)
                throw new System.InvalidOperationException("Missing Convoy lamp material.");
            float y = bounds.min.y + bounds.size.y * 0.28f;
            float front = bounds.max.z + 0.035f;
            for (int side = -1; side <= 1; side += 2)
                PrototypeGeometry.Part("Enemy headlamp", enemy.visualRoot,
                    new Vector3(side * bounds.extents.x * 0.72f, y, front),
                    new Vector3(0.42f, 0.17f, 0.055f), glow, false);
            enemy.headlights = AddLight("Enemy headlight pool", enemy.visualRoot,
                new Vector3(0f, y, front), LightType.Spot, 6f, 30f);
            enemy.headlights.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            enemy.headlights.enabled = false;
        }

        private static Light AddLight(string name, Transform parent, Vector3 position,
            LightType type, float intensity, float range)
        {
            Light light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = type;
            light.color = WarmLight;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = 65f;
            light.innerSpotAngle = 40f;
            light.shadows = LightShadows.None;
            return light;
        }
    }
}
