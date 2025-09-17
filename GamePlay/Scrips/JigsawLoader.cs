using UnityEngine;
using System.IO;
using UnityEngine.UI;

[System.Serializable]
public class JigsawRow
{
    public string[] cols;
}

[System.Serializable]
public class JigsawLevel
{
    public int rows;
    public int cols;
    public JigsawRow[] layout;
}

public class JigsawLoader : MonoBehaviour
{
    [Header("Jigsaw Setup")]
    public JigsawPieceDatabase pieceDatabase; // assign in Inspector (15 shapes)
    public GameObject piecePrefab;            // prefab with UI Image + optional Mask
    public Transform parentTransform;         // board parent (Grid holder)
    public float pieceSize = 100f;            // spacing in UI units
    public string jsonFileName = "level1.json";

    [Header("Source Image")]
    public Texture2D sourceImage;             // The full image (e.g. butterfly 600x600)

    private void Start()
    {
        LoadLevel(jsonFileName);
    }

    /// <summary>
    /// Main loader
    /// </summary>
    public void LoadLevel(string fileName)
    {
        // Load JSON from StreamingAssets
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(path))
        {
            Debug.LogError("Level JSON not found: " + path);
            return;
        }

        string json = File.ReadAllText(path);
        JigsawLevel level = JsonUtility.FromJson<JigsawLevel>(json);

        // Cut source image into slices
        Sprite[,] cutSprites = CutImage(sourceImage, level.rows, level.cols);

        // Generate grid
        for (int r = 0; r < level.rows; r++)
        {
            for (int c = 0; c < level.cols; c++)
            {
                string code = level.layout[r].cols[c];
                if (string.IsNullOrEmpty(code)) continue;

                int pieceIndex = int.Parse(code.Substring(0, 2)); // shape index (0–14)
                int rotation = int.Parse(code.Substring(2, 1));   // rotation (0–3)

                // Instantiate prefab
                // Instantiate prefab
                GameObject obj = Instantiate(piecePrefab, parentTransform);
                obj.transform.localPosition = new Vector3(c * pieceSize, -r * pieceSize, 0);

                // Set cropped image (always upright)
                var img = obj.GetComponent<Image>();
                if (img != null)
                    img.sprite = cutSprites[r, c];

                // Apply jigsaw mask shape
                if (pieceDatabase != null && pieceIndex < pieceDatabase.pieces.Length)
                {
                    var maskImage = obj.transform.GetChild(0).GetComponent<Image>();
                    if (maskImage != null)
                    {
                        maskImage.sprite = pieceDatabase.pieces[pieceIndex].sprite;
                        maskImage.transform.localRotation = Quaternion.Euler(0, 0, rotation * 90); // rotate mask only
                    }
                }

                Debug.Log($"Placed piece {pieceIndex} at ({r},{c}) with rotation {rotation * 90}°");

            }
        }
    }

    /// <summary>
    /// Cuts a texture into grid pieces and returns them as Sprites
    /// </summary>
    private Sprite[,] CutImage(Texture2D fullImage, int rows, int cols)
    {
        int pieceWidth = fullImage.width / cols;
        int pieceHeight = fullImage.height / rows;

        Sprite[,] pieces = new Sprite[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                // JSON row 0 = top row
                int y = (rows - 1 - r) * pieceHeight; // flip vertically
                int x = c * pieceWidth;

                Rect rect = new Rect(x, y, pieceWidth, pieceHeight);
                Sprite sprite = Sprite.Create(fullImage, rect, new Vector2(0.5f, 0.5f), 100f);
                pieces[r, c] = sprite;
            }
        }

        return pieces;
    }


}
