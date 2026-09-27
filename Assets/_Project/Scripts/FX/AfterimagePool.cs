using UnityEngine;

namespace Margin.FX
{
    /// <summary>
    /// Dash afterimages (spec 4.3): fading grey copies of the stick figure's current lines.
    /// A small fixed pool of ghost objects is reused, oldest first.
    /// </summary>
    public sealed class AfterimagePool
    {
        private sealed class Ghost
        {
            public GameObject Root;
            public LineRenderer[] Lines;
            public int Life;
        }

        private readonly Ghost[] ghosts;
        private int next;
        private readonly int fadeFrames;
        private readonly float alpha;
        private readonly Color tint;

        public AfterimagePool(int count, int lineCount, int fadeFrames, float alpha, Color tint, Material material, int sortingOrder)
        {
            this.fadeFrames = Mathf.Max(1, fadeFrames);
            this.alpha = alpha;
            this.tint = tint;
            ghosts = new Ghost[Mathf.Max(1, count)];
            for (int g = 0; g < ghosts.Length; g++)
            {
                var root = new GameObject("Afterimage") { hideFlags = HideFlags.DontSave };
                var lines = new LineRenderer[lineCount];
                for (int i = 0; i < lineCount; i++)
                {
                    var child = new GameObject("Line");
                    child.transform.SetParent(root.transform, false);
                    LineRenderer line = child.AddComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.sharedMaterial = material;
                    line.numCapVertices = 4;
                    line.numCornerVertices = 3;
                    line.sortingOrder = sortingOrder;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lines[i] = line;
                }
                root.SetActive(false);
                ghosts[g] = new Ghost { Root = root, Lines = lines };
            }
        }

        public int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (Ghost g in ghosts) if (g.Life > 0) n++;
                return n;
            }
        }

        /// <summary>Copies the given lines (positions, widths, loop) into the next ghost.</summary>
        public void Spawn(LineRenderer[] source)
        {
            Ghost ghost = ghosts[next];
            next = (next + 1) % ghosts.Length;

            var buffer = new Vector3[64];
            for (int i = 0; i < ghost.Lines.Length; i++)
            {
                LineRenderer target = ghost.Lines[i];
                LineRenderer from = i < source.Length ? source[i] : null;
                if (from == null)
                {
                    target.positionCount = 0;
                    continue;
                }
                int n = Mathf.Min(from.positionCount, buffer.Length);
                from.GetPositions(buffer);
                target.positionCount = n;
                target.SetPositions(buffer);
                target.loop = from.loop;
                target.widthMultiplier = from.widthMultiplier;
                target.widthCurve = from.widthCurve;
            }
            ghost.Life = fadeFrames;
            ghost.Root.SetActive(true);
            SetAlpha(ghost, alpha);
        }

        /// <summary>Call once per gameplay tick to fade ghosts out.</summary>
        public void Tick()
        {
            foreach (Ghost ghost in ghosts)
            {
                if (ghost.Life <= 0) continue;
                ghost.Life--;
                if (ghost.Life == 0) ghost.Root.SetActive(false);
                else SetAlpha(ghost, alpha * ghost.Life / fadeFrames);
            }
        }

        public void Destroy()
        {
            foreach (Ghost ghost in ghosts)
                if (ghost.Root != null) Object.Destroy(ghost.Root);
        }

        private void SetAlpha(Ghost ghost, float a)
        {
            Color c = tint;
            c.a = a;
            foreach (LineRenderer line in ghost.Lines)
            {
                line.startColor = c;
                line.endColor = c;
            }
        }
    }
}
