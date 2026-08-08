using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class GradientBox : VisualElement
{
    [UxmlAttribute] private Color startColor { get; set; } = Color.red;
    [UxmlAttribute] private Color endColor { get; set; } = Color.blue;
    [UxmlAttribute] private bool horizontal { get; set; } = false;

    public GradientBox()
    {
        generateVisualContent += OnGenerateVisualContent;
    }

    private void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        var rect = contentRect;
        if (rect.width <= 0 || rect.height <= 0) return;

        var mesh = mgc.Allocate(4, 6);

        var topLeft     = new Vector3(rect.xMin, rect.yMin, 0);
        var topRight    = new Vector3(rect.xMax, rect.yMin, 0);
        var bottomLeft  = new Vector3(rect.xMin, rect.yMax, 0);
        var bottomRight = new Vector3(rect.xMax, rect.yMax, 0);

        var cTL = startColor;
        var cTR = horizontal ? endColor : startColor;
        var cBL = horizontal ? startColor : endColor;
        var cBR = endColor;

        mesh.SetNextVertex(new Vertex() { position = topLeft,     tint = cTL, uv = new Vector2(0, 1) });
        mesh.SetNextVertex(new Vertex() { position = topRight,    tint = cTR, uv = new Vector2(1, 1) });
        mesh.SetNextVertex(new Vertex() { position = bottomLeft,  tint = cBL, uv = new Vector2(0, 0) });
        mesh.SetNextVertex(new Vertex() { position = bottomRight, tint = cBR, uv = new Vector2(1, 0) });

        mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
        mesh.SetNextIndex(2); mesh.SetNextIndex(1); mesh.SetNextIndex(3);
    }
}