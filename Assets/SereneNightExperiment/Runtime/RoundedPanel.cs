using UnityEngine;
using UnityEngine.UI;

// Native UGUI geometry: no texture, shader or external UI package required.
[RequireComponent(typeof(CanvasRenderer))]
public class RoundedPanel : MaskableGraphic
{
    public float radius = 14;
    public float borderWidth = 1.5f;
    public Color borderColor = new Color(.46f, .59f, .51f, .55f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = rectTransform.rect;
        float r = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) / 2);
        float width = Mathf.Min(borderWidth, r);
        vh.AddVert(rect.center, color, Vector2.zero);
        const int segments = 9;
        const int count = 4 * segments;
        for (int corner = 0; corner < 4; corner++) {
            var center = new Vector2(corner < 2 ? rect.xMax - r : rect.xMin + r,
                corner == 0 || corner == 3 ? rect.yMax - r : rect.yMin + r);
            for (int i = 0; i < segments; i++) {
                float angle = (90 - corner * 90 - i * 90f / (segments - 1)) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(center + direction * (r - width), color, Vector2.zero);
                vh.AddVert(center + direction * r, borderColor, Vector2.zero);
            }
        }
        for (int i = 0; i < count; i++) {
            int a = 1 + i * 2, b = 1 + ((i + 1) % count) * 2;
            vh.AddTriangle(0, a, b);
            vh.AddTriangle(a, a + 1, b + 1);
            vh.AddTriangle(a, b + 1, b);
        }
    }
}
