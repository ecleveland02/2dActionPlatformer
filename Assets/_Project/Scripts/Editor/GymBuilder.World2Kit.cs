using Margin.Combat;
using Margin.Enemies;
using Margin.Physics;
using Margin.Rendering;
using UnityEngine;

namespace Margin.EditorTools
{
    /// <summary>Builders for World 2's pieces: Tack Turrets and Eraser Crawlers.</summary>
    public static partial class GymBuilder
    {
        /// <summary>A Tack Turret centered at <paramref name="position"/>, stuck to a surface facing <paramref name="mount"/>.</summary>
        private static GameObject BuildTurret(Vector3 position, Vector2 mount, GymAssets assets, EnemyData data)
        {
            var go = new GameObject("Tack Turret");
            go.transform.position = position;
            var body = go.AddComponent<KinematicBody2D>();
            go.GetComponent<BoxCollider2D>().size = new Vector2(0.7f, 0.7f);
            var rb = go.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            SetReference(body, "data", assets.BodyData);
            go.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(0.75f, 0.75f));

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(go.transform, false);
            var visual = visualObject.AddComponent<TackTurretVisual>();
            SetReference(visual, "lineMaterial", assets.Ink);

            var turret = go.AddComponent<TackTurret>();
            SetReference(turret, "data", data);
            SetReference(turret, "physics", assets.Movement);
            SetReference(turret, "settings", assets.Combat.Settings);
            turret.ConfigureTurret(mount, visual);
            return go;
        }

        /// <summary>An Eraser Crawler standing with its feet at <paramref name="feet"/>.</summary>
        private static GameObject BuildCrawler(Vector3 feet, GymAssets assets, EnemyData data)
        {
            const float height = 0.62f;
            var go = new GameObject("Eraser Crawler");
            go.transform.position = feet + new Vector3(0f, height * 0.5f + 0.02f, 0f);
            var body = go.AddComponent<KinematicBody2D>();
            go.GetComponent<BoxCollider2D>().size = new Vector2(0.85f, height);
            var rb = go.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            SetReference(body, "data", assets.BodyData);
            go.AddComponent<Hurtbox>().Configure(Faction.Enemy, Vector2.zero, new Vector2(0.9f, height));

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(go.transform, false);
            visualObject.transform.localPosition = new Vector3(0f, -height * 0.5f, 0f);
            visualObject.transform.localScale = new Vector3(-1f, 1f, 1f);
            var visual = visualObject.AddComponent<EraserVisual>();
            SetReference(visual, "lineMaterial", assets.Ink);

            var crawler = go.AddComponent<EraserCrawler>();
            SetReference(crawler, "data", data);
            SetReference(crawler, "physics", assets.Movement);
            SetReference(crawler, "settings", assets.Combat.Settings);
            crawler.ConfigureVisual(visual);
            return go;
        }
    }
}
